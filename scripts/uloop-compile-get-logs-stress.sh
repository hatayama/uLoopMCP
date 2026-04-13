#!/bin/sh
# Stress test: repeatedly run uloop compile and uloop get-logs in sequence.
# Designed for long-running Unity server restart/recovery verification.

set -eu

PROJECT_PATH="${ULOOP_PROJECT_PATH:-}"
INTERVAL_SECONDS="${ULOOP_STRESS_INTERVAL_SECONDS:-2}"
LOG_DIR="${ULOOP_STRESS_LOG_DIR:-.uloop/stress-tests}"
MAX_ROUNDS="${ULOOP_STRESS_MAX_ROUNDS:-0}"
WAIT_FOR_READY_SECONDS="${ULOOP_STRESS_WAIT_FOR_READY_SECONDS:-15}"
mkdir -p "$LOG_DIR"

timestamp() {
    date +"%Y-%m-%dT%H:%M:%S%z"
}

uloop_cmd() {
    if [ -n "$PROJECT_PATH" ]; then
        uloop "$@" --project-path "$PROJECT_PATH"
    else
        uloop "$@"
    fi
}

wait_for_ready() {
    round="$1"
    deadline=$(( $(date +%s) + WAIT_FOR_READY_SECONDS ))
    while :; do
        if uloop_cmd get-logs --max-count 1 >/dev/null 2>&1; then
            printf '%s [%s] ready\n' "$(timestamp)" "$round"
            return 0
        fi

        now="$(date +%s)"
        if [ "$now" -ge "$deadline" ]; then
            printf '%s [%s] ready timeout after %ss\n' "$(timestamp)" "$round" "$WAIT_FOR_READY_SECONDS"
            return 1
        fi

        sleep 2
    done
}

cleanup() {
    printf '%s cleanup\n' "$(timestamp)"
}

trap cleanup INT TERM

echo "=== uloop compile/get-logs stress test ==="
echo "log_dir=$LOG_DIR"
echo "interval_seconds=$INTERVAL_SECONDS"
echo "max_rounds=$MAX_ROUNDS"
echo "wait_for_ready_seconds=$WAIT_FOR_READY_SECONDS"

if ! wait_for_ready "bootstrap"; then
    echo "bootstrap failed"
    exit 1
fi

round=1
while :; do
    if [ "$MAX_ROUNDS" -gt 0 ] && [ "$round" -gt "$MAX_ROUNDS" ]; then
        echo "reached max rounds: $MAX_ROUNDS"
        exit 0
    fi

    if ! uloop_cmd compile >"$LOG_DIR/${round}_compile.out" 2>"$LOG_DIR/${round}_compile.err"; then
        echo "compile failed at round $round"
        exit 1
    fi

    if ! wait_for_ready "$round"; then
        echo "server did not become ready after compile at round $round"
        exit 1
    fi

    if ! uloop_cmd get-logs --max-count 1 >"$LOG_DIR/${round}_get-logs.out" 2>"$LOG_DIR/${round}_get-logs.err"; then
        echo "get-logs failed at round $round"
        exit 1
    fi

    echo "$(timestamp) [$round] round complete"
    round=$((round + 1))
    sleep "$INTERVAL_SECONDS"
done
