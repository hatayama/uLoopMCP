#!/bin/sh
# E2E verification that screenshot --annotate-elements lists UI Toolkit controls and that simulate-mouse-ui and
# simulate-mouse-input press them at the annotated points, using the uloop CLI against
# UIToolkitAnnotationDemoScene. Safe to run repeatedly: it stops PlayMode and restores the Game View size on
# exit, and never saves the scene, so the listener and EventSystem changes made in PlayMode are discarded.

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
    # The six named controls plus the list's vertical Scroller; parts inside a control would add more.
    count=$(jq '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit")] | length' "$E1_JSON")
    assert_text_equals "$count" "7" "UI Toolkit element count"
    play_path=$(jq -r '[.Screenshots[0].AnnotatedElements[] | select(.UiSystem == "UIToolkit" and .Name == "play")][0].Path' "$E1_JSON")
    assert_text_equals "$play_path" "UIToolkitAnnotationDemo/BackDocument#play" "UI Toolkit element 'play' path"
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

get_click_count() {
    name=$1
    json=$(run_uloop_json execute-dynamic-code --code "
UIToolkitAnnotationDemo demo = UnityEngine.Object.FindObjectOfType<UIToolkitAnnotationDemo>();
if (demo == null) { return \"ERROR: UIToolkitAnnotationDemo not found\"; }
return demo.GetClickCount(\"$name\");
")
    assert_json_success "$json" "Read $name click count"
    count=$(printf '%s\n' "$json" | jq -r '.Result')
    case "$count" in
        ERROR:*)
            fail "Read $name click count failed: ${count#ERROR: }"
            ;;
    esac
    printf '%s\n' "$count"
}

# Prints a field of the first annotated element matching the jq selector.
annotated_field() {
    selector=$1
    field=$2
    jq -r "[.Screenshots[0].AnnotatedElements[] | select($selector)][0].$field" "$E2_JSON"
}

assert_response_field() {
    json=$1
    field=$2
    expected=$3
    context=$4
    assert_text_equals "$(printf '%s\n' "$json" | jq -r ".$field")" "$expected" "$context $field"
}

# Prints the simulate-mouse-ui response after checking it succeeded.
simulate_ui() {
    context=$1
    shift
    json=$(run_uloop_json simulate-mouse-ui "$@")
    assert_json_success "$json" "$context"
    printf '%s\n' "$json"
}

PLAY_SELECTOR='.UiSystem == "UIToolkit" and .Name == "play"'

# simulate-mouse-ui reaches the UI Toolkit button directly and reports it, including for a click inside a
# scrollbar, where it must report the scrollbar rather than the part inside it.
verify_ui_click() {
    x=$(annotated_field "$PLAY_SELECTOR" SimX)
    y=$(annotated_field "$PLAY_SELECTOR" SimY)
    before=$(get_click_count play)

    json=$(simulate_ui "simulate-mouse-ui click on play" --action Click --x "$x" --y "$y")
    assert_response_field "$json" UiSystem "UIToolkit" "simulate-mouse-ui click on play"
    assert_response_field "$json" HitPath "UIToolkitAnnotationDemo/BackDocument#play" "simulate-mouse-ui click on play"
    assert_response_field "$json" HitGameObjectName "play" "simulate-mouse-ui click on play"
    assert_text_equals "$(get_click_count play)" "$((before + 1))" "play click count after simulate-mouse-ui"

    scrollbar_selector='.UiSystem == "UIToolkit" and .Type == "Scrollbar"'
    json=$(simulate_ui "simulate-mouse-ui click on the scrollbar" --action Click \
        --x "$(annotated_field "$scrollbar_selector" SimX)" --y "$(annotated_field "$scrollbar_selector" SimY)")
    assert_response_field "$json" HitPath "$(annotated_field "$scrollbar_selector" Path)" "simulate-mouse-ui click on the scrollbar"
}

# simulate-mouse-input reaches UI Toolkit buttons through the EventSystem's Input System UI module.
verify_input_click() {
    before=$(get_click_count play)

    json=$(run_uloop_json simulate-mouse-input --action Click \
        --x "$(annotated_field "$PLAY_SELECTOR" SimX)" --y "$(annotated_field "$PLAY_SELECTOR" SimY)")
    assert_json_success "$json" "simulate-mouse-input click on play"
    sleep 1

    assert_text_equals "$(get_click_count play)" "$((before + 1))" "play click count after simulate-mouse-input"
}

# The front panel takes a click on the covered part; the uncovered annotated point reaches the button.
verify_covered_click() {
    selector='.UiSystem == "UIToolkit" and .Name == "partly-covered"'
    min_x=$(annotated_field "$selector" BoundsMinX)
    max_x=$(annotated_field "$selector" BoundsMaxX)
    min_y=$(annotated_field "$selector" BoundsMinY)
    max_y=$(annotated_field "$selector" BoundsMaxY)
    covered_x=$(jq -n --argjson min "$min_x" --argjson max "$max_x" '$min + ($max - $min) * 0.3')
    covered_y=$(jq -n --argjson min "$min_y" --argjson max "$max_y" '($min + $max) / 2')
    before=$(get_click_count partly-covered)

    json=$(simulate_ui "simulate-mouse-ui click on the covered part" --action Click --x "$covered_x" --y "$covered_y")
    assert_response_field "$json" HitPath "UIToolkitAnnotationDemo/FrontDocument#cover" "simulate-mouse-ui click on the covered part"
    assert_text_equals "$(get_click_count partly-covered)" "$before" "partly-covered click count after clicking its cover"

    simulate_ui "simulate-mouse-ui click on partly-covered" --action Click \
        --x "$(annotated_field "$selector" SimX)" --y "$(annotated_field "$selector" SimY)" >/dev/null
    assert_text_equals "$(get_click_count partly-covered)" "$((before + 1))" "partly-covered click count"
}

# The disabled button is not annotated, so its layout centre in the demo (100,440 200x50) is used. The press is
# delivered but, as with real input, the disabled button does not react.
verify_disabled_click() {
    json=$(simulate_ui "simulate-mouse-ui click on locked" --action Click --x 200 --y 465)
    assert_response_field "$json" HitPath "UIToolkitAnnotationDemo/BackDocument#locked" "simulate-mouse-ui click on locked"
    assert_text_equals "$(get_click_count locked)" "0" "locked click count"
}

verify_long_press() {
    before=$(get_click_count play)

    simulate_ui "simulate-mouse-ui long press on play" --action LongPress --duration 0.5 \
        --x "$(annotated_field "$PLAY_SELECTOR" SimX)" --y "$(annotated_field "$PLAY_SELECTOR" SimY)" >/dev/null

    assert_text_equals "$(get_click_count play)" "$((before + 1))" "play click count after long press"
}

# The demo does not count uGUI clicks, so a listener added in PlayMode writes the count into the button label.
verify_ugui_click() {
    json=$(run_uloop_json execute-dynamic-code --code '
using UnityEngine;
using UnityEngine.UI;
GameObject buttonObject = GameObject.Find("ugui-button");
if (buttonObject == null) { return "ERROR: ugui-button not found"; }
Text label = buttonObject.GetComponentInChildren<Text>();
if (label == null) { return "ERROR: ugui-button has no Text"; }
int clicks = 0;
buttonObject.GetComponent<Button>().onClick.AddListener(() => { clicks++; label.text = "clicked " + clicks; });
label.text = "clicked 0";
return label.text;
')
    assert_json_success "$json" "Add ugui-button listener"
    assert_text_equals "$(printf '%s\n' "$json" | jq -r '.Result')" "clicked 0" "ugui-button listener"

    selector='.UiSystem == "UGUI" and .Name == "ugui-button"'
    json=$(simulate_ui "simulate-mouse-ui click on ugui-button" --action Click \
        --x "$(annotated_field "$selector" SimX)" --y "$(annotated_field "$selector" SimY)")
    assert_response_field "$json" UiSystem "UGUI" "simulate-mouse-ui click on ugui-button"

    json=$(run_uloop_json execute-dynamic-code --code '
using UnityEngine;
using UnityEngine.UI;
return GameObject.Find("ugui-button").GetComponentInChildren<Text>().text;
')
    assert_json_success "$json" "Read ugui-button label"
    assert_text_equals "$(printf '%s\n' "$json" | jq -r '.Result')" "clicked 1" "ugui-button label"
}

# Runs last: the EventSystem stays disabled until PlayMode stops on exit.
verify_click_without_event_system() {
    json=$(run_uloop_json execute-dynamic-code --code '
using UnityEngine.EventSystems;
if (EventSystem.current == null) { return "ERROR: no EventSystem"; }
EventSystem.current.gameObject.SetActive(false);
return EventSystem.current == null ? "disabled" : "ERROR: still active";
')
    assert_json_success "$json" "Disable EventSystem"
    assert_text_equals "$(printf '%s\n' "$json" | jq -r '.Result')" "disabled" "EventSystem"
    before=$(get_click_count play)

    simulate_ui "simulate-mouse-ui click on play without an EventSystem" --action Click \
        --x "$(annotated_field "$PLAY_SELECTOR" SimX)" --y "$(annotated_field "$PLAY_SELECTOR" SimY)" >/dev/null

    assert_text_equals "$(get_click_count play)" "$((before + 1))" "play click count without an EventSystem"
}

rm -rf "$TMP_DIR"
mkdir -p "$TMP_DIR"
require_jq

printf '\n'
printf '=========================================\n'
printf '  Screenshot UI Toolkit Annotation E2E\n'
printf '=========================================\n'

wait_unity_ready

printf '[1/12] Loading UI Toolkit annotation demo scene...\n'
initialize_demo_scene

printf '[2/12] Selecting Full HD Game View resolution...\n'
select_full_hd_game_view

printf '[3/12] Starting PlayMode and waiting for UI Toolkit controls...\n'
run_uloop_json control-play-mode --action Play >/dev/null
wait_play_mode
wait_for_uitoolkit_play

printf '[4/12] Checking the annotated capture...\n'
verify_annotated_capture

printf '[5/12] Checking the elements-only capture matches...\n'
verify_elements_only_capture

printf '[6/12] Clicking UI Toolkit controls with simulate-mouse-ui...\n'
verify_ui_click

printf '[7/12] Clicking a UI Toolkit button with simulate-mouse-input...\n'
verify_input_click

printf '[8/12] Clicking a partly covered UI Toolkit button...\n'
verify_covered_click

printf '[9/12] Clicking a disabled UI Toolkit button...\n'
verify_disabled_click

printf '[10/12] Long-pressing a UI Toolkit button...\n'
verify_long_press

printf '[11/12] Clicking the uGUI button with simulate-mouse-ui...\n'
verify_ugui_click

printf '[12/12] Clicking a UI Toolkit button without an EventSystem...\n'
verify_click_without_event_system

printf '\n'
printf '=========================================\n'
printf '  RESULT: PASS\n'
printf '=========================================\n'
