import { getProjectResolutionErrorLines } from '../cli-project-error.js';
import { UnityNotRunningError } from '../port-resolver.js';
import { ProjectMismatchError } from '../project-validator.js';

describe('getProjectResolutionErrorLines', () => {
  it('returns not-running guidance for UnityNotRunningError', () => {
    const lines = getProjectResolutionErrorLines(new UnityNotRunningError('/project/root'));

    expect(lines).toEqual([
      'Error: Unity Editor for this project is not running.',
      '',
      '  Project: /project/root',
      '',
      'Start the Unity Editor for this project and try again.',
    ]);
  });

  it('returns mismatch guidance for ProjectMismatchError', () => {
    const lines = getProjectResolutionErrorLines(
      new ProjectMismatchError('/expected/project', '/connected/project'),
    );

    expect(lines).toEqual([
      'Error: Connected Unity instance belongs to a different project.',
      '',
      '  Project:      /expected/project',
      '  Connected to: /connected/project',
      '',
      'Another Unity instance was found, but it belongs to a different project.',
      'Start the Unity Editor for this project, or use --project-path to specify the target.',
    ]);
  });
});
