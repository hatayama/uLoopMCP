#!/bin/sh
# E2E verification that screenshot --annotate-elements lists UI Toolkit controls, using the uloop CLI against
# UIToolkitAnnotationDemoScene. Safe to run repeatedly: it stops PlayMode and restores the Game View size on
# exit, and never saves the scene.

set -eu

SCENE_PATH="Assets/Scenes/UIToolkitAnnotationDemoScene.unity"
TMP_DIR="${TMPDIR:-/tmp}/unity-cli-loop-uitk-annotation"
E1_JSON="$TMP_DIR/annotated.json"
E2_JSON="$TMP_DIR/elements-only.json"
POLL_JSON="$TMP_DIR/poll.json"
ORIGINAL_GAME_VIEW_WIDTH=""
ORIGINAL_GAME_VIEW_HEIGHT=""
PROJECT_PATH=""
ULOOP_PATH="${ULOOP_BIN:-uloop}"

fail() {
    printf 'ERROR: %s\n' "$1" >&2
    exit 1
}

cleanup() {
    run_uloop control-play-mode --action Stop >/dev/null 2>&1 || true
    if [ -n "${ORIGINAL_GAME_VIEW_WIDTH:-}" ] && [ -n "${ORIGINAL_GAME_VIEW_HEIGHT:-}" ]; then
        run_uloop set-game-view-size \
            --width "$ORIGINAL_GAME_VIEW_WIDTH" \
            --height "$ORIGINAL_GAME_VIEW_HEIGHT" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT INT TERM

while [ "$#" -gt 0 ]; do
    case "$1" in
        --project-path)
            [ "$#" -ge 2 ] || fail "--project-path requires a value"
            PROJECT_PATH=$2
            shift 2
            ;;
        --uloop-path)
            [ "$#" -ge 2 ] || fail "--uloop-path requires a value"
            ULOOP_PATH=$2
            shift 2
            ;;
        -h|--help)
            printf 'Usage: sh scripts/test-screenshot-uitoolkit-annotation.sh [--project-path <path>] [--uloop-path <path>]\n'
            exit 0
            ;;
        *)
            fail "unknown option: $1"
            ;;
    esac
done

require_jq() {
    command -v jq >/dev/null 2>&1 || fail "jq is required to parse uloop JSON responses"
}

run_uloop() {
    if [ -n "$PROJECT_PATH" ]; then
        "$ULOOP_PATH" --project-path "$PROJECT_PATH" "$@"
        return
    fi

    "$ULOOP_PATH" "$@"
}

run_uloop_json() {
    if ! output=$(run_uloop "$@" 2>&1); then
        printf '%s\n' "$output" >&2
        fail "uloop $* failed"
    fi

    printf '%s\n' "$output"
}

assert_json_success() {
    json=$1
    context=$2

    printf '%s\n' "$json" | jq -e '.Success == true' >/dev/null \
        || fail "$context failed: $(printf '%s\n' "$json" | jq -r '.ErrorMessage // .Message // .Error // "unknown error"')"
}

assert_text_equals() {
    actual=$1
    expected=$2
    context=$3

    [ "$actual" = "$expected" ] || fail "$context: expected '$expected', got '$actual'"
}

# Fails with the message unless the jq filter is true for the file.
assert_jq() {
    file=$1
    filter=$2
    message=$3

    jq -e "$filter" "$file" >/dev/null || fail "$message"
}

wait_unity_ready() {
    attempt=0
    while [ "$attempt" -lt 15 ]; do
        if run_uloop get-logs --max-count 1 >/dev/null 2>&1; then
            return
        fi

        attempt=$((attempt + 1))
        sleep 2
    done

    fail "Unity did not respond to uloop"
}

wait_play_mode() {
    attempt=0
    while [ "$attempt" -lt 20 ]; do
        json=$(run_uloop_json execute-dynamic-code --code 'using UnityEngine; return Application.isPlaying;')
        if printf '%s\n' "$json" | jq -e '.Success == true and .Result == "True"' >/dev/null; then
            return
        fi

        attempt=$((attempt + 1))
        sleep 1
    done

    fail "Unity did not enter PlayMode"
}

# Opening the scene in Single mode closes every loaded scene, so unsaved changes anywhere stop the run instead
# of being discarded.
initialize_demo_scene() {
    run_uloop control-play-mode --action Stop >/dev/null 2>&1 || true

    code="
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
for (int i = 0; i < SceneManager.sceneCount; i++)
{
    Scene loaded = SceneManager.GetSceneAt(i);
    if (loaded.isLoaded && loaded.isDirty) { return \"DIRTY: \" + loaded.path; }
}
PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
if (prefabStage != null && prefabStage.scene.isDirty) { return \"DIRTY: \" + prefabStage.assetPath; }
string scenePath = \"$SCENE_PATH\";
if (SceneManager.GetActiveScene().path != scenePath)
{
    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
}
return SceneManager.GetActiveScene().path;
"

    json=$(run_uloop_json execute-dynamic-code --code "$code")
    assert_json_success "$json" "Load demo scene"
    scene=$(printf '%s\n' "$json" | jq -r '.Result')
    case "$scene" in
        DIRTY:*)
            fail "Unsaved changes would be lost by opening the demo scene (${scene#DIRTY: }). Save or discard them first."
            ;;
    esac
    assert_text_equals "$scene" "$SCENE_PATH" "Active scene"
}

select_full_hd_game_view() {
    json=$(run_uloop_json set-game-view-size --width 1920 --height 1080)
    assert_json_success "$json" "Set Full HD Game View"
    ORIGINAL_GAME_VIEW_WIDTH=$(printf '%s\n' "$json" | jq -r '.PreviousWidth')
    ORIGINAL_GAME_VIEW_HEIGHT=$(printf '%s\n' "$json" | jq -r '.PreviousHeight')
    assert_text_equals "$(printf '%s\n' "$json" | jq -r '.CurrentWidth')" "1920" "Game View width"
    assert_text_equals "$(printf '%s\n' "$json" | jq -r '.CurrentHeight')" "1080" "Game View height"
    printf '    1920x1080 (previous: %sx%s)\n' "$ORIGINAL_GAME_VIEW_WIDTH" "$ORIGINAL_GAME_VIEW_HEIGHT"
}

# The demo builds its controls in Start, so the first captures after entering PlayMode can miss them.
wait_for_uitoolkit_play() {
    attempt=0
    while [ "$attempt" -lt 20 ]; do
        run_uloop_json screenshot --capture-mode rendering --annotate-elements --elements-only >"$POLL_JSON"
        if jq -e '[.Screenshots[0].AnnotatedElements[]? | select(.UiSystem == "UIToolkit" and .Name == "play")] | length > 0' \
            "$POLL_JSON" >/dev/null; then
            return
        fi

        attempt=$((attempt + 1))
        sleep 1
    done

    fail "UI Toolkit element 'play' did not appear"
}

assert_single_uitoolkit_element() {
    file=$1
    name=$2
    type=$3
    interaction=$4

    count=$(jq --arg name "$name" \
        '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit" and .Name == $name)] | length' "$file")
    [ "$count" = "1" ] || fail "Expected exactly one UI Toolkit element named '$name', found $count"
    assert_jq "$file" \
        "[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == \"UIToolkit\" and .Name == \"$name\")][0]
            | .Type == \"$type\" and .Interaction == \"$interaction\"" \
        "UI Toolkit element '$name' should be $type / $interaction"
}

verify_annotated_capture() {
    run_uloop_json screenshot \
        --capture-mode rendering \
        --annotate-elements \
        --output-directory "$TMP_DIR" >"$E1_JSON"
    assert_jq "$E1_JSON" '.Success == true and .ScreenshotCount == 1' "Annotated capture failed"

    assert_single_uitoolkit_element "$E1_JSON" "play" "Button" "Click"
    assert_single_uitoolkit_element "$E1_JSON" "sound" "Toggle" "Click"
    assert_single_uitoolkit_element "$E1_JSON" "volume" "Slider" "Drag"
    assert_single_uitoolkit_element "$E1_JSON" "player-name" "InputField" "Text"
    assert_single_uitoolkit_element "$E1_JSON" "quality" "Dropdown" "Click"
    assert_single_uitoolkit_element "$E1_JSON" "partly-covered" "Button" "Click"
    assert_jq "$E1_JSON" \
        '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit" and .Type == "Scrollbar")] | length == 1' \
        "Expected exactly one UI Toolkit Scrollbar"
    assert_jq "$E1_JSON" \
        '[.Screenshots[0].AnnotatedElements[] | select(.Name == "locked" or .Name == "ghost")] | length == 0' \
        "Disabled or hidden UI Toolkit buttons were annotated"

    # Top-left Game View coordinates: a wrong Y flip moves these away from the layout in the demo.
    assert_jq "$E1_JSON" \
        '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit" and .Name == "play")][0]
            | ((.BoundsMinX - 100) | fabs) <= 1 and ((.BoundsMaxX - 300) | fabs) <= 1
              and ((.BoundsMinY - 100) | fabs) <= 1 and ((.BoundsMaxY - 150) | fabs) <= 1' \
        "UI Toolkit element 'play' bounds should be 100,100 to 300,150"
    assert_jq "$E1_JSON" \
        '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit" and .Name == "partly-covered")][0]
            | .SimX > .BoundsMinX + 0.6 * (.BoundsMaxX - .BoundsMinX)' \
        "UI Toolkit element 'partly-covered' should be clicked right of the panel covering its left 60%"
    assert_jq "$E1_JSON" \
        '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit")]
            | all((.Path | contains("#"))
                  and .BoundsMinX <= .SimX and .SimX <= .BoundsMaxX
                  and .BoundsMinY <= .SimY and .SimY <= .BoundsMaxY)' \
        "Every UI Toolkit element needs a '#' path and a click point inside its bounds"
    assert_jq "$E1_JSON" \
        '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UGUI" and .Name == "ugui-button")] | length == 1' \
        "Expected exactly one uGUI element named 'ugui-button'"

    image_path=$(jq -r '.Screenshots[0].ImagePath' "$E1_JSON")
    [ -s "$image_path" ] || fail "Annotated screenshot was not written: $image_path"
}

# Compares the path set first so an element missing only from the elements-only capture is not overlooked.
verify_elements_only_capture() {
    run_uloop_json screenshot --capture-mode rendering --annotate-elements --elements-only >"$E2_JSON"
    assert_jq "$E2_JSON" '.Success == true and .ScreenshotCount == 1' "Elements-only capture failed"

    paths_filter='[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit") | .Path] | sort'
    e1_paths=$(jq -c "$paths_filter" "$E1_JSON")
    e2_paths=$(jq -c "$paths_filter" "$E2_JSON")
    assert_text_equals "$e2_paths" "$e1_paths" "UI Toolkit element paths in the elements-only capture"

    jq -e -n --slurpfile e1 "$E1_JSON" --slurpfile e2 "$E2_JSON" '
        def uitk($doc): [$doc[0].Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit")];
        def close($a; $b): (($a - $b) | fabs) <= 1;
        uitk($e1) as $first
        | uitk($e2)
        | all(. as $second
              | ($first[] | select(.Path == $second.Path)) as $match
              | close($match.SimX; $second.SimX) and close($match.SimY; $second.SimY)
                and close($match.BoundsMinX; $second.BoundsMinX) and close($match.BoundsMaxX; $second.BoundsMaxX)
                and close($match.BoundsMinY; $second.BoundsMinY) and close($match.BoundsMaxY; $second.BoundsMaxY))' \
        >/dev/null || fail "Elements-only coordinates differ from the annotated capture by more than 1 pixel"
}

get_play_click_count() {
    json=$(run_uloop_json execute-dynamic-code --code '
UIToolkitAnnotationDemo demo = UnityEngine.Object.FindObjectOfType<UIToolkitAnnotationDemo>();
if (demo == null) { return "ERROR: UIToolkitAnnotationDemo not found"; }
return demo.GetClickCount("play");
')
    assert_json_success "$json" "Read play click count"
    printf '%s\n' "$json" | jq -r '.Result'
}

# simulate-mouse-input reaches UI Toolkit buttons through the EventSystem; simulate-mouse-ui does not.
verify_click() {
    x=$(jq -r '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit" and .Name == "play")][0].SimX' "$E1_JSON")
    y=$(jq -r '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit" and .Name == "play")][0].SimY' "$E1_JSON")
    before=$(get_play_click_count)

    json=$(run_uloop_json simulate-mouse-input --action Click --x "$x" --y "$y")
    assert_json_success "$json" "Click play"
    sleep 1

    after=$(get_play_click_count)
    assert_text_equals "$after" "$((before + 1))" "play click count"
}

rm -rf "$TMP_DIR"
mkdir -p "$TMP_DIR"
require_jq

printf '\n'
printf '=========================================\n'
printf '  Screenshot UI Toolkit Annotation E2E\n'
printf '=========================================\n'

wait_unity_ready

printf '[1/6] Loading UI Toolkit annotation demo scene...\n'
initialize_demo_scene

printf '[2/6] Selecting Full HD Game View resolution...\n'
select_full_hd_game_view

printf '[3/6] Starting PlayMode and waiting for UI Toolkit controls...\n'
run_uloop_json control-play-mode --action Play >/dev/null
wait_play_mode
wait_for_uitoolkit_play

printf '[4/6] Checking the annotated capture...\n'
verify_annotated_capture

printf '[5/6] Checking the elements-only capture matches...\n'
verify_elements_only_capture

printf '[6/6] Clicking a UI Toolkit button at its annotated point...\n'
verify_click

printf '\n'
printf '=========================================\n'
printf '  RESULT: PASS\n'
printf '=========================================\n'
