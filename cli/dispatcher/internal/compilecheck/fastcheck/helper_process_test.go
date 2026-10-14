package fastcheck

import (
	"errors"
	"fmt"
	"os"
	"os/exec"
	"strconv"
	"testing"
	"time"
)

// helperExitCodeVariable makes the test binary exit with the given code instead of running tests,
// so a test can obtain the error a real process exit produces.
const helperExitCodeVariable = "FASTCHECK_TEST_HELPER_EXIT_CODE"

// helperSleepVariable makes the test binary wait until it is killed instead of running tests.
const helperSleepVariable = "FASTCHECK_TEST_HELPER_SLEEP"

func TestMain(m *testing.M) {
	if code := os.Getenv(helperExitCodeVariable); code != "" {
		exitCode, err := strconv.Atoi(code)
		if err != nil {
			os.Exit(1)
		}

		os.Exit(exitCode)
	}

	if os.Getenv(helperSleepVariable) != "" {
		time.Sleep(time.Minute)
		os.Exit(0)
	}

	os.Exit(m.Run())
}

// exitErrorWithCode returns the error a process that ran and exited with code produces.
func exitErrorWithCode(code int) error {
	command := exec.Command(os.Args[0])
	command.Env = append(os.Environ(), fmt.Sprintf("%s=%d", helperExitCodeVariable, code))
	err := command.Run()
	var exitError *exec.ExitError
	if !errors.As(err, &exitError) {
		panic(fmt.Sprintf("expected the helper process to exit with %d, got %v", code, err))
	}

	return err
}

// signaledExitError returns the error a process killed by a signal produces.
func signaledExitError() error {
	command := exec.Command(os.Args[0])
	command.Env = append(os.Environ(), helperSleepVariable+"=1")
	if err := command.Start(); err != nil {
		panic(fmt.Sprintf("failed to start the helper process: %v", err))
	}

	_ = command.Process.Kill()
	err := command.Wait()
	var exitError *exec.ExitError
	if !errors.As(err, &exitError) {
		panic(fmt.Sprintf("expected the killed helper process to report an exit error, got %v", err))
	}

	return err
}
