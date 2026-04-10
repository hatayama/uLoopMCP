import { UnityNotRunningError, UnityServerNotRunningError } from './port-resolver.js';
import { ProjectMismatchError } from './project-validator.js';

export function getProjectResolutionErrorLines(
  error: UnityNotRunningError | UnityServerNotRunningError | ProjectMismatchError,
): string[] {
  if (error instanceof UnityServerNotRunningError) {
    return [
      'Error: Unity Editor is running, but Unity CLI Loop server is not.',
      '',
      `  Project: ${error.projectRoot}`,
      '',
      'Start the server from: Window > Unity CLI Loop > Server',
    ];
  }

  if (error instanceof UnityNotRunningError) {
    return [
      'Error: Unity Editor for this project is not running.',
      '',
      `  Project: ${error.projectRoot}`,
      '',
      'Start the Unity Editor for this project and try again.',
    ];
  }

  return [
    'Error: Connected Unity instance belongs to a different project.',
    '',
    `  Project:      ${error.expectedProjectRoot}`,
    `  Connected to: ${error.connectedProjectRoot}`,
    '',
    'Another Unity instance was found, but it belongs to a different project.',
    'Start the Unity Editor for this project, or use --project-path to specify the target.',
  ];
}
