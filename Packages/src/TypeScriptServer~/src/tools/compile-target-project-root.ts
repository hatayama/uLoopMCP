export function resolveCompileTargetProjectRoot(
  finalResult: unknown,
  projectRootFromUnity: string | undefined,
  fallbackProjectRoot: string,
): string {
  if (typeof finalResult === 'object' && finalResult !== null) {
    const resultRecord = finalResult as Record<string, unknown>;
    const projectRoot = resultRecord['ProjectRoot'];
    if (typeof projectRoot === 'string' && projectRoot.length > 0) {
      return projectRoot;
    }
  }

  if (typeof projectRootFromUnity === 'string' && projectRootFromUnity.length > 0) {
    return projectRootFromUnity;
  }

  return fallbackProjectRoot;
}
