/**
 * Jest transformer for .md files
 * Converts markdown files to string exports (similar to esbuild's --loader:.md=text)
 */
module.exports = {
  process(sourceText) {
    return {
      code: `module.exports = ${JSON.stringify(sourceText)};`,
    };
  },
};
