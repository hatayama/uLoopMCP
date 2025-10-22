#!/usr/bin/env node
var __defProp = Object.defineProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};

// node_modules/zod/dist/esm/v3/external.js
var external_exports = {};
__export(external_exports, {
  BRAND: () => BRAND,
  DIRTY: () => DIRTY,
  EMPTY_PATH: () => EMPTY_PATH,
  INVALID: () => INVALID,
  NEVER: () => NEVER,
  OK: () => OK,
  ParseStatus: () => ParseStatus,
  Schema: () => ZodType,
  ZodAny: () => ZodAny,
  ZodArray: () => ZodArray,
  ZodBigInt: () => ZodBigInt,
  ZodBoolean: () => ZodBoolean,
  ZodBranded: () => ZodBranded,
  ZodCatch: () => ZodCatch,
  ZodDate: () => ZodDate,
  ZodDefault: () => ZodDefault,
  ZodDiscriminatedUnion: () => ZodDiscriminatedUnion,
  ZodEffects: () => ZodEffects,
  ZodEnum: () => ZodEnum,
  ZodError: () => ZodError,
  ZodFirstPartyTypeKind: () => ZodFirstPartyTypeKind,
  ZodFunction: () => ZodFunction,
  ZodIntersection: () => ZodIntersection,
  ZodIssueCode: () => ZodIssueCode,
  ZodLazy: () => ZodLazy,
  ZodLiteral: () => ZodLiteral,
  ZodMap: () => ZodMap,
  ZodNaN: () => ZodNaN,
  ZodNativeEnum: () => ZodNativeEnum,
  ZodNever: () => ZodNever,
  ZodNull: () => ZodNull,
  ZodNullable: () => ZodNullable,
  ZodNumber: () => ZodNumber,
  ZodObject: () => ZodObject,
  ZodOptional: () => ZodOptional,
  ZodParsedType: () => ZodParsedType,
  ZodPipeline: () => ZodPipeline,
  ZodPromise: () => ZodPromise,
  ZodReadonly: () => ZodReadonly,
  ZodRecord: () => ZodRecord,
  ZodSchema: () => ZodType,
  ZodSet: () => ZodSet,
  ZodString: () => ZodString,
  ZodSymbol: () => ZodSymbol,
  ZodTransformer: () => ZodEffects,
  ZodTuple: () => ZodTuple,
  ZodType: () => ZodType,
  ZodUndefined: () => ZodUndefined,
  ZodUnion: () => ZodUnion,
  ZodUnknown: () => ZodUnknown,
  ZodVoid: () => ZodVoid,
  addIssueToContext: () => addIssueToContext,
  any: () => anyType,
  array: () => arrayType,
  bigint: () => bigIntType,
  boolean: () => booleanType,
  coerce: () => coerce,
  custom: () => custom,
  date: () => dateType,
  datetimeRegex: () => datetimeRegex,
  defaultErrorMap: () => en_default,
  discriminatedUnion: () => discriminatedUnionType,
  effect: () => effectsType,
  enum: () => enumType,
  function: () => functionType,
  getErrorMap: () => getErrorMap,
  getParsedType: () => getParsedType,
  instanceof: () => instanceOfType,
  intersection: () => intersectionType,
  isAborted: () => isAborted,
  isAsync: () => isAsync,
  isDirty: () => isDirty,
  isValid: () => isValid,
  late: () => late,
  lazy: () => lazyType,
  literal: () => literalType,
  makeIssue: () => makeIssue,
  map: () => mapType,
  nan: () => nanType,
  nativeEnum: () => nativeEnumType,
  never: () => neverType,
  null: () => nullType,
  nullable: () => nullableType,
  number: () => numberType,
  object: () => objectType,
  objectUtil: () => objectUtil,
  oboolean: () => oboolean,
  onumber: () => onumber,
  optional: () => optionalType,
  ostring: () => ostring,
  pipeline: () => pipelineType,
  preprocess: () => preprocessType,
  promise: () => promiseType,
  quotelessJson: () => quotelessJson,
  record: () => recordType,
  set: () => setType,
  setErrorMap: () => setErrorMap,
  strictObject: () => strictObjectType,
  string: () => stringType,
  symbol: () => symbolType,
  transformer: () => effectsType,
  tuple: () => tupleType,
  undefined: () => undefinedType,
  union: () => unionType,
  unknown: () => unknownType,
  util: () => util,
  void: () => voidType
});

// node_modules/zod/dist/esm/v3/helpers/util.js
var util;
(function(util2) {
  util2.assertEqual = (_) => {
  };
  function assertIs(_arg) {
  }
  util2.assertIs = assertIs;
  function assertNever(_x) {
    throw new Error();
  }
  util2.assertNever = assertNever;
  util2.arrayToEnum = (items) => {
    const obj = {};
    for (const item of items) {
      obj[item] = item;
    }
    return obj;
  };
  util2.getValidEnumValues = (obj) => {
    const validKeys = util2.objectKeys(obj).filter((k) => typeof obj[obj[k]] !== "number");
    const filtered = {};
    for (const k of validKeys) {
      filtered[k] = obj[k];
    }
    return util2.objectValues(filtered);
  };
  util2.objectValues = (obj) => {
    return util2.objectKeys(obj).map(function(e) {
      return obj[e];
    });
  };
  util2.objectKeys = typeof Object.keys === "function" ? (obj) => Object.keys(obj) : (object) => {
    const keys = [];
    for (const key in object) {
      if (Object.prototype.hasOwnProperty.call(object, key)) {
        keys.push(key);
      }
    }
    return keys;
  };
  util2.find = (arr, checker) => {
    for (const item of arr) {
      if (checker(item))
        return item;
    }
    return void 0;
  };
  util2.isInteger = typeof Number.isInteger === "function" ? (val) => Number.isInteger(val) : (val) => typeof val === "number" && Number.isFinite(val) && Math.floor(val) === val;
  function joinValues(array, separator = " | ") {
    return array.map((val) => typeof val === "string" ? `'${val}'` : val).join(separator);
  }
  util2.joinValues = joinValues;
  util2.jsonStringifyReplacer = (_, value) => {
    if (typeof value === "bigint") {
      return value.toString();
    }
    return value;
  };
})(util || (util = {}));
var objectUtil;
(function(objectUtil2) {
  objectUtil2.mergeShapes = (first, second) => {
    return {
      ...first,
      ...second
      // second overwrites first
    };
  };
})(objectUtil || (objectUtil = {}));
var ZodParsedType = util.arrayToEnum([
  "string",
  "nan",
  "number",
  "integer",
  "float",
  "boolean",
  "date",
  "bigint",
  "symbol",
  "function",
  "undefined",
  "null",
  "array",
  "object",
  "unknown",
  "promise",
  "void",
  "never",
  "map",
  "set"
]);
var getParsedType = (data) => {
  const t = typeof data;
  switch (t) {
    case "undefined":
      return ZodParsedType.undefined;
    case "string":
      return ZodParsedType.string;
    case "number":
      return Number.isNaN(data) ? ZodParsedType.nan : ZodParsedType.number;
    case "boolean":
      return ZodParsedType.boolean;
    case "function":
      return ZodParsedType.function;
    case "bigint":
      return ZodParsedType.bigint;
    case "symbol":
      return ZodParsedType.symbol;
    case "object":
      if (Array.isArray(data)) {
        return ZodParsedType.array;
      }
      if (data === null) {
        return ZodParsedType.null;
      }
      if (data.then && typeof data.then === "function" && data.catch && typeof data.catch === "function") {
        return ZodParsedType.promise;
      }
      if (typeof Map !== "undefined" && data instanceof Map) {
        return ZodParsedType.map;
      }
      if (typeof Set !== "undefined" && data instanceof Set) {
        return ZodParsedType.set;
      }
      if (typeof Date !== "undefined" && data instanceof Date) {
        return ZodParsedType.date;
      }
      return ZodParsedType.object;
    default:
      return ZodParsedType.unknown;
  }
};

// node_modules/zod/dist/esm/v3/ZodError.js
var ZodIssueCode = util.arrayToEnum([
  "invalid_type",
  "invalid_literal",
  "custom",
  "invalid_union",
  "invalid_union_discriminator",
  "invalid_enum_value",
  "unrecognized_keys",
  "invalid_arguments",
  "invalid_return_type",
  "invalid_date",
  "invalid_string",
  "too_small",
  "too_big",
  "invalid_intersection_types",
  "not_multiple_of",
  "not_finite"
]);
var quotelessJson = (obj) => {
  const json = JSON.stringify(obj, null, 2);
  return json.replace(/"([^"]+)":/g, "$1:");
};
var ZodError = class _ZodError extends Error {
  get errors() {
    return this.issues;
  }
  constructor(issues) {
    super();
    this.issues = [];
    this.addIssue = (sub) => {
      this.issues = [...this.issues, sub];
    };
    this.addIssues = (subs = []) => {
      this.issues = [...this.issues, ...subs];
    };
    const actualProto = new.target.prototype;
    if (Object.setPrototypeOf) {
      Object.setPrototypeOf(this, actualProto);
    } else {
      this.__proto__ = actualProto;
    }
    this.name = "ZodError";
    this.issues = issues;
  }
  format(_mapper) {
    const mapper = _mapper || function(issue) {
      return issue.message;
    };
    const fieldErrors = { _errors: [] };
    const processError = (error) => {
      for (const issue of error.issues) {
        if (issue.code === "invalid_union") {
          issue.unionErrors.map(processError);
        } else if (issue.code === "invalid_return_type") {
          processError(issue.returnTypeError);
        } else if (issue.code === "invalid_arguments") {
          processError(issue.argumentsError);
        } else if (issue.path.length === 0) {
          fieldErrors._errors.push(mapper(issue));
        } else {
          let curr = fieldErrors;
          let i = 0;
          while (i < issue.path.length) {
            const el = issue.path[i];
            const terminal = i === issue.path.length - 1;
            if (!terminal) {
              curr[el] = curr[el] || { _errors: [] };
            } else {
              curr[el] = curr[el] || { _errors: [] };
              curr[el]._errors.push(mapper(issue));
            }
            curr = curr[el];
            i++;
          }
        }
      }
    };
    processError(this);
    return fieldErrors;
  }
  static assert(value) {
    if (!(value instanceof _ZodError)) {
      throw new Error(`Not a ZodError: ${value}`);
    }
  }
  toString() {
    return this.message;
  }
  get message() {
    return JSON.stringify(this.issues, util.jsonStringifyReplacer, 2);
  }
  get isEmpty() {
    return this.issues.length === 0;
  }
  flatten(mapper = (issue) => issue.message) {
    const fieldErrors = {};
    const formErrors = [];
    for (const sub of this.issues) {
      if (sub.path.length > 0) {
        fieldErrors[sub.path[0]] = fieldErrors[sub.path[0]] || [];
        fieldErrors[sub.path[0]].push(mapper(sub));
      } else {
        formErrors.push(mapper(sub));
      }
    }
    return { formErrors, fieldErrors };
  }
  get formErrors() {
    return this.flatten();
  }
};
ZodError.create = (issues) => {
  const error = new ZodError(issues);
  return error;
};

// node_modules/zod/dist/esm/v3/locales/en.js
var errorMap = (issue, _ctx) => {
  let message;
  switch (issue.code) {
    case ZodIssueCode.invalid_type:
      if (issue.received === ZodParsedType.undefined) {
        message = "Required";
      } else {
        message = `Expected ${issue.expected}, received ${issue.received}`;
      }
      break;
    case ZodIssueCode.invalid_literal:
      message = `Invalid literal value, expected ${JSON.stringify(issue.expected, util.jsonStringifyReplacer)}`;
      break;
    case ZodIssueCode.unrecognized_keys:
      message = `Unrecognized key(s) in object: ${util.joinValues(issue.keys, ", ")}`;
      break;
    case ZodIssueCode.invalid_union:
      message = `Invalid input`;
      break;
    case ZodIssueCode.invalid_union_discriminator:
      message = `Invalid discriminator value. Expected ${util.joinValues(issue.options)}`;
      break;
    case ZodIssueCode.invalid_enum_value:
      message = `Invalid enum value. Expected ${util.joinValues(issue.options)}, received '${issue.received}'`;
      break;
    case ZodIssueCode.invalid_arguments:
      message = `Invalid function arguments`;
      break;
    case ZodIssueCode.invalid_return_type:
      message = `Invalid function return type`;
      break;
    case ZodIssueCode.invalid_date:
      message = `Invalid date`;
      break;
    case ZodIssueCode.invalid_string:
      if (typeof issue.validation === "object") {
        if ("includes" in issue.validation) {
          message = `Invalid input: must include "${issue.validation.includes}"`;
          if (typeof issue.validation.position === "number") {
            message = `${message} at one or more positions greater than or equal to ${issue.validation.position}`;
          }
        } else if ("startsWith" in issue.validation) {
          message = `Invalid input: must start with "${issue.validation.startsWith}"`;
        } else if ("endsWith" in issue.validation) {
          message = `Invalid input: must end with "${issue.validation.endsWith}"`;
        } else {
          util.assertNever(issue.validation);
        }
      } else if (issue.validation !== "regex") {
        message = `Invalid ${issue.validation}`;
      } else {
        message = "Invalid";
      }
      break;
    case ZodIssueCode.too_small:
      if (issue.type === "array")
        message = `Array must contain ${issue.exact ? "exactly" : issue.inclusive ? `at least` : `more than`} ${issue.minimum} element(s)`;
      else if (issue.type === "string")
        message = `String must contain ${issue.exact ? "exactly" : issue.inclusive ? `at least` : `over`} ${issue.minimum} character(s)`;
      else if (issue.type === "number")
        message = `Number must be ${issue.exact ? `exactly equal to ` : issue.inclusive ? `greater than or equal to ` : `greater than `}${issue.minimum}`;
      else if (issue.type === "date")
        message = `Date must be ${issue.exact ? `exactly equal to ` : issue.inclusive ? `greater than or equal to ` : `greater than `}${new Date(Number(issue.minimum))}`;
      else
        message = "Invalid input";
      break;
    case ZodIssueCode.too_big:
      if (issue.type === "array")
        message = `Array must contain ${issue.exact ? `exactly` : issue.inclusive ? `at most` : `less than`} ${issue.maximum} element(s)`;
      else if (issue.type === "string")
        message = `String must contain ${issue.exact ? `exactly` : issue.inclusive ? `at most` : `under`} ${issue.maximum} character(s)`;
      else if (issue.type === "number")
        message = `Number must be ${issue.exact ? `exactly` : issue.inclusive ? `less than or equal to` : `less than`} ${issue.maximum}`;
      else if (issue.type === "bigint")
        message = `BigInt must be ${issue.exact ? `exactly` : issue.inclusive ? `less than or equal to` : `less than`} ${issue.maximum}`;
      else if (issue.type === "date")
        message = `Date must be ${issue.exact ? `exactly` : issue.inclusive ? `smaller than or equal to` : `smaller than`} ${new Date(Number(issue.maximum))}`;
      else
        message = "Invalid input";
      break;
    case ZodIssueCode.custom:
      message = `Invalid input`;
      break;
    case ZodIssueCode.invalid_intersection_types:
      message = `Intersection results could not be merged`;
      break;
    case ZodIssueCode.not_multiple_of:
      message = `Number must be a multiple of ${issue.multipleOf}`;
      break;
    case ZodIssueCode.not_finite:
      message = "Number must be finite";
      break;
    default:
      message = _ctx.defaultError;
      util.assertNever(issue);
  }
  return { message };
};
var en_default = errorMap;

// node_modules/zod/dist/esm/v3/errors.js
var overrideErrorMap = en_default;
function setErrorMap(map) {
  overrideErrorMap = map;
}
function getErrorMap() {
  return overrideErrorMap;
}

// node_modules/zod/dist/esm/v3/helpers/parseUtil.js
var makeIssue = (params) => {
  const { data, path: path2, errorMaps, issueData } = params;
  const fullPath = [...path2, ...issueData.path || []];
  const fullIssue = {
    ...issueData,
    path: fullPath
  };
  if (issueData.message !== void 0) {
    return {
      ...issueData,
      path: fullPath,
      message: issueData.message
    };
  }
  let errorMessage = "";
  const maps = errorMaps.filter((m) => !!m).slice().reverse();
  for (const map of maps) {
    errorMessage = map(fullIssue, { data, defaultError: errorMessage }).message;
  }
  return {
    ...issueData,
    path: fullPath,
    message: errorMessage
  };
};
var EMPTY_PATH = [];
function addIssueToContext(ctx, issueData) {
  const overrideMap = getErrorMap();
  const issue = makeIssue({
    issueData,
    data: ctx.data,
    path: ctx.path,
    errorMaps: [
      ctx.common.contextualErrorMap,
      // contextual error map is first priority
      ctx.schemaErrorMap,
      // then schema-bound map if available
      overrideMap,
      // then global override map
      overrideMap === en_default ? void 0 : en_default
      // then global default map
    ].filter((x) => !!x)
  });
  ctx.common.issues.push(issue);
}
var ParseStatus = class _ParseStatus {
  constructor() {
    this.value = "valid";
  }
  dirty() {
    if (this.value === "valid")
      this.value = "dirty";
  }
  abort() {
    if (this.value !== "aborted")
      this.value = "aborted";
  }
  static mergeArray(status, results) {
    const arrayValue = [];
    for (const s of results) {
      if (s.status === "aborted")
        return INVALID;
      if (s.status === "dirty")
        status.dirty();
      arrayValue.push(s.value);
    }
    return { status: status.value, value: arrayValue };
  }
  static async mergeObjectAsync(status, pairs) {
    const syncPairs = [];
    for (const pair of pairs) {
      const key = await pair.key;
      const value = await pair.value;
      syncPairs.push({
        key,
        value
      });
    }
    return _ParseStatus.mergeObjectSync(status, syncPairs);
  }
  static mergeObjectSync(status, pairs) {
    const finalObject = {};
    for (const pair of pairs) {
      const { key, value } = pair;
      if (key.status === "aborted")
        return INVALID;
      if (value.status === "aborted")
        return INVALID;
      if (key.status === "dirty")
        status.dirty();
      if (value.status === "dirty")
        status.dirty();
      if (key.value !== "__proto__" && (typeof value.value !== "undefined" || pair.alwaysSet)) {
        finalObject[key.value] = value.value;
      }
    }
    return { status: status.value, value: finalObject };
  }
};
var INVALID = Object.freeze({
  status: "aborted"
});
var DIRTY = (value) => ({ status: "dirty", value });
var OK = (value) => ({ status: "valid", value });
var isAborted = (x) => x.status === "aborted";
var isDirty = (x) => x.status === "dirty";
var isValid = (x) => x.status === "valid";
var isAsync = (x) => typeof Promise !== "undefined" && x instanceof Promise;

// node_modules/zod/dist/esm/v3/helpers/errorUtil.js
var errorUtil;
(function(errorUtil2) {
  errorUtil2.errToObj = (message) => typeof message === "string" ? { message } : message || {};
  errorUtil2.toString = (message) => typeof message === "string" ? message : message?.message;
})(errorUtil || (errorUtil = {}));

// node_modules/zod/dist/esm/v3/types.js
var ParseInputLazyPath = class {
  constructor(parent, value, path2, key) {
    this._cachedPath = [];
    this.parent = parent;
    this.data = value;
    this._path = path2;
    this._key = key;
  }
  get path() {
    if (!this._cachedPath.length) {
      if (Array.isArray(this._key)) {
        this._cachedPath.push(...this._path, ...this._key);
      } else {
        this._cachedPath.push(...this._path, this._key);
      }
    }
    return this._cachedPath;
  }
};
var handleResult = (ctx, result) => {
  if (isValid(result)) {
    return { success: true, data: result.value };
  } else {
    if (!ctx.common.issues.length) {
      throw new Error("Validation failed but no issues detected.");
    }
    return {
      success: false,
      get error() {
        if (this._error)
          return this._error;
        const error = new ZodError(ctx.common.issues);
        this._error = error;
        return this._error;
      }
    };
  }
};
function processCreateParams(params) {
  if (!params)
    return {};
  const { errorMap: errorMap2, invalid_type_error, required_error, description } = params;
  if (errorMap2 && (invalid_type_error || required_error)) {
    throw new Error(`Can't use "invalid_type_error" or "required_error" in conjunction with custom error map.`);
  }
  if (errorMap2)
    return { errorMap: errorMap2, description };
  const customMap = (iss, ctx) => {
    const { message } = params;
    if (iss.code === "invalid_enum_value") {
      return { message: message ?? ctx.defaultError };
    }
    if (typeof ctx.data === "undefined") {
      return { message: message ?? required_error ?? ctx.defaultError };
    }
    if (iss.code !== "invalid_type")
      return { message: ctx.defaultError };
    return { message: message ?? invalid_type_error ?? ctx.defaultError };
  };
  return { errorMap: customMap, description };
}
var ZodType = class {
  get description() {
    return this._def.description;
  }
  _getType(input) {
    return getParsedType(input.data);
  }
  _getOrReturnCtx(input, ctx) {
    return ctx || {
      common: input.parent.common,
      data: input.data,
      parsedType: getParsedType(input.data),
      schemaErrorMap: this._def.errorMap,
      path: input.path,
      parent: input.parent
    };
  }
  _processInputParams(input) {
    return {
      status: new ParseStatus(),
      ctx: {
        common: input.parent.common,
        data: input.data,
        parsedType: getParsedType(input.data),
        schemaErrorMap: this._def.errorMap,
        path: input.path,
        parent: input.parent
      }
    };
  }
  _parseSync(input) {
    const result = this._parse(input);
    if (isAsync(result)) {
      throw new Error("Synchronous parse encountered promise.");
    }
    return result;
  }
  _parseAsync(input) {
    const result = this._parse(input);
    return Promise.resolve(result);
  }
  parse(data, params) {
    const result = this.safeParse(data, params);
    if (result.success)
      return result.data;
    throw result.error;
  }
  safeParse(data, params) {
    const ctx = {
      common: {
        issues: [],
        async: params?.async ?? false,
        contextualErrorMap: params?.errorMap
      },
      path: params?.path || [],
      schemaErrorMap: this._def.errorMap,
      parent: null,
      data,
      parsedType: getParsedType(data)
    };
    const result = this._parseSync({ data, path: ctx.path, parent: ctx });
    return handleResult(ctx, result);
  }
  "~validate"(data) {
    const ctx = {
      common: {
        issues: [],
        async: !!this["~standard"].async
      },
      path: [],
      schemaErrorMap: this._def.errorMap,
      parent: null,
      data,
      parsedType: getParsedType(data)
    };
    if (!this["~standard"].async) {
      try {
        const result = this._parseSync({ data, path: [], parent: ctx });
        return isValid(result) ? {
          value: result.value
        } : {
          issues: ctx.common.issues
        };
      } catch (err) {
        if (err?.message?.toLowerCase()?.includes("encountered")) {
          this["~standard"].async = true;
        }
        ctx.common = {
          issues: [],
          async: true
        };
      }
    }
    return this._parseAsync({ data, path: [], parent: ctx }).then((result) => isValid(result) ? {
      value: result.value
    } : {
      issues: ctx.common.issues
    });
  }
  async parseAsync(data, params) {
    const result = await this.safeParseAsync(data, params);
    if (result.success)
      return result.data;
    throw result.error;
  }
  async safeParseAsync(data, params) {
    const ctx = {
      common: {
        issues: [],
        contextualErrorMap: params?.errorMap,
        async: true
      },
      path: params?.path || [],
      schemaErrorMap: this._def.errorMap,
      parent: null,
      data,
      parsedType: getParsedType(data)
    };
    const maybeAsyncResult = this._parse({ data, path: ctx.path, parent: ctx });
    const result = await (isAsync(maybeAsyncResult) ? maybeAsyncResult : Promise.resolve(maybeAsyncResult));
    return handleResult(ctx, result);
  }
  refine(check, message) {
    const getIssueProperties = (val) => {
      if (typeof message === "string" || typeof message === "undefined") {
        return { message };
      } else if (typeof message === "function") {
        return message(val);
      } else {
        return message;
      }
    };
    return this._refinement((val, ctx) => {
      const result = check(val);
      const setError = () => ctx.addIssue({
        code: ZodIssueCode.custom,
        ...getIssueProperties(val)
      });
      if (typeof Promise !== "undefined" && result instanceof Promise) {
        return result.then((data) => {
          if (!data) {
            setError();
            return false;
          } else {
            return true;
          }
        });
      }
      if (!result) {
        setError();
        return false;
      } else {
        return true;
      }
    });
  }
  refinement(check, refinementData) {
    return this._refinement((val, ctx) => {
      if (!check(val)) {
        ctx.addIssue(typeof refinementData === "function" ? refinementData(val, ctx) : refinementData);
        return false;
      } else {
        return true;
      }
    });
  }
  _refinement(refinement) {
    return new ZodEffects({
      schema: this,
      typeName: ZodFirstPartyTypeKind.ZodEffects,
      effect: { type: "refinement", refinement }
    });
  }
  superRefine(refinement) {
    return this._refinement(refinement);
  }
  constructor(def) {
    this.spa = this.safeParseAsync;
    this._def = def;
    this.parse = this.parse.bind(this);
    this.safeParse = this.safeParse.bind(this);
    this.parseAsync = this.parseAsync.bind(this);
    this.safeParseAsync = this.safeParseAsync.bind(this);
    this.spa = this.spa.bind(this);
    this.refine = this.refine.bind(this);
    this.refinement = this.refinement.bind(this);
    this.superRefine = this.superRefine.bind(this);
    this.optional = this.optional.bind(this);
    this.nullable = this.nullable.bind(this);
    this.nullish = this.nullish.bind(this);
    this.array = this.array.bind(this);
    this.promise = this.promise.bind(this);
    this.or = this.or.bind(this);
    this.and = this.and.bind(this);
    this.transform = this.transform.bind(this);
    this.brand = this.brand.bind(this);
    this.default = this.default.bind(this);
    this.catch = this.catch.bind(this);
    this.describe = this.describe.bind(this);
    this.pipe = this.pipe.bind(this);
    this.readonly = this.readonly.bind(this);
    this.isNullable = this.isNullable.bind(this);
    this.isOptional = this.isOptional.bind(this);
    this["~standard"] = {
      version: 1,
      vendor: "zod",
      validate: (data) => this["~validate"](data)
    };
  }
  optional() {
    return ZodOptional.create(this, this._def);
  }
  nullable() {
    return ZodNullable.create(this, this._def);
  }
  nullish() {
    return this.nullable().optional();
  }
  array() {
    return ZodArray.create(this);
  }
  promise() {
    return ZodPromise.create(this, this._def);
  }
  or(option) {
    return ZodUnion.create([this, option], this._def);
  }
  and(incoming) {
    return ZodIntersection.create(this, incoming, this._def);
  }
  transform(transform) {
    return new ZodEffects({
      ...processCreateParams(this._def),
      schema: this,
      typeName: ZodFirstPartyTypeKind.ZodEffects,
      effect: { type: "transform", transform }
    });
  }
  default(def) {
    const defaultValueFunc = typeof def === "function" ? def : () => def;
    return new ZodDefault({
      ...processCreateParams(this._def),
      innerType: this,
      defaultValue: defaultValueFunc,
      typeName: ZodFirstPartyTypeKind.ZodDefault
    });
  }
  brand() {
    return new ZodBranded({
      typeName: ZodFirstPartyTypeKind.ZodBranded,
      type: this,
      ...processCreateParams(this._def)
    });
  }
  catch(def) {
    const catchValueFunc = typeof def === "function" ? def : () => def;
    return new ZodCatch({
      ...processCreateParams(this._def),
      innerType: this,
      catchValue: catchValueFunc,
      typeName: ZodFirstPartyTypeKind.ZodCatch
    });
  }
  describe(description) {
    const This = this.constructor;
    return new This({
      ...this._def,
      description
    });
  }
  pipe(target) {
    return ZodPipeline.create(this, target);
  }
  readonly() {
    return ZodReadonly.create(this);
  }
  isOptional() {
    return this.safeParse(void 0).success;
  }
  isNullable() {
    return this.safeParse(null).success;
  }
};
var cuidRegex = /^c[^\s-]{8,}$/i;
var cuid2Regex = /^[0-9a-z]+$/;
var ulidRegex = /^[0-9A-HJKMNP-TV-Z]{26}$/i;
var uuidRegex = /^[0-9a-fA-F]{8}\b-[0-9a-fA-F]{4}\b-[0-9a-fA-F]{4}\b-[0-9a-fA-F]{4}\b-[0-9a-fA-F]{12}$/i;
var nanoidRegex = /^[a-z0-9_-]{21}$/i;
var jwtRegex = /^[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]*$/;
var durationRegex = /^[-+]?P(?!$)(?:(?:[-+]?\d+Y)|(?:[-+]?\d+[.,]\d+Y$))?(?:(?:[-+]?\d+M)|(?:[-+]?\d+[.,]\d+M$))?(?:(?:[-+]?\d+W)|(?:[-+]?\d+[.,]\d+W$))?(?:(?:[-+]?\d+D)|(?:[-+]?\d+[.,]\d+D$))?(?:T(?=[\d+-])(?:(?:[-+]?\d+H)|(?:[-+]?\d+[.,]\d+H$))?(?:(?:[-+]?\d+M)|(?:[-+]?\d+[.,]\d+M$))?(?:[-+]?\d+(?:[.,]\d+)?S)?)??$/;
var emailRegex = /^(?!\.)(?!.*\.\.)([A-Z0-9_'+\-\.]*)[A-Z0-9_+-]@([A-Z0-9][A-Z0-9\-]*\.)+[A-Z]{2,}$/i;
var _emojiRegex = `^(\\p{Extended_Pictographic}|\\p{Emoji_Component})+$`;
var emojiRegex;
var ipv4Regex = /^(?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9][0-9]|[0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9][0-9]|[0-9])$/;
var ipv4CidrRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9][0-9]|[0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9][0-9]|[0-9])\/(3[0-2]|[12]?[0-9])$/;
var ipv6Regex = /^(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))$/;
var ipv6CidrRegex = /^(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))\/(12[0-8]|1[01][0-9]|[1-9]?[0-9])$/;
var base64Regex = /^([0-9a-zA-Z+/]{4})*(([0-9a-zA-Z+/]{2}==)|([0-9a-zA-Z+/]{3}=))?$/;
var base64urlRegex = /^([0-9a-zA-Z-_]{4})*(([0-9a-zA-Z-_]{2}(==)?)|([0-9a-zA-Z-_]{3}(=)?))?$/;
var dateRegexSource = `((\\d\\d[2468][048]|\\d\\d[13579][26]|\\d\\d0[48]|[02468][048]00|[13579][26]00)-02-29|\\d{4}-((0[13578]|1[02])-(0[1-9]|[12]\\d|3[01])|(0[469]|11)-(0[1-9]|[12]\\d|30)|(02)-(0[1-9]|1\\d|2[0-8])))`;
var dateRegex = new RegExp(`^${dateRegexSource}$`);
function timeRegexSource(args) {
  let secondsRegexSource = `[0-5]\\d`;
  if (args.precision) {
    secondsRegexSource = `${secondsRegexSource}\\.\\d{${args.precision}}`;
  } else if (args.precision == null) {
    secondsRegexSource = `${secondsRegexSource}(\\.\\d+)?`;
  }
  const secondsQuantifier = args.precision ? "+" : "?";
  return `([01]\\d|2[0-3]):[0-5]\\d(:${secondsRegexSource})${secondsQuantifier}`;
}
function timeRegex(args) {
  return new RegExp(`^${timeRegexSource(args)}$`);
}
function datetimeRegex(args) {
  let regex = `${dateRegexSource}T${timeRegexSource(args)}`;
  const opts = [];
  opts.push(args.local ? `Z?` : `Z`);
  if (args.offset)
    opts.push(`([+-]\\d{2}:?\\d{2})`);
  regex = `${regex}(${opts.join("|")})`;
  return new RegExp(`^${regex}$`);
}
function isValidIP(ip, version) {
  if ((version === "v4" || !version) && ipv4Regex.test(ip)) {
    return true;
  }
  if ((version === "v6" || !version) && ipv6Regex.test(ip)) {
    return true;
  }
  return false;
}
function isValidJWT(jwt, alg) {
  if (!jwtRegex.test(jwt))
    return false;
  try {
    const [header] = jwt.split(".");
    const base64 = header.replace(/-/g, "+").replace(/_/g, "/").padEnd(header.length + (4 - header.length % 4) % 4, "=");
    const decoded = JSON.parse(atob(base64));
    if (typeof decoded !== "object" || decoded === null)
      return false;
    if ("typ" in decoded && decoded?.typ !== "JWT")
      return false;
    if (!decoded.alg)
      return false;
    if (alg && decoded.alg !== alg)
      return false;
    return true;
  } catch {
    return false;
  }
}
function isValidCidr(ip, version) {
  if ((version === "v4" || !version) && ipv4CidrRegex.test(ip)) {
    return true;
  }
  if ((version === "v6" || !version) && ipv6CidrRegex.test(ip)) {
    return true;
  }
  return false;
}
var ZodString = class _ZodString extends ZodType {
  _parse(input) {
    if (this._def.coerce) {
      input.data = String(input.data);
    }
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.string) {
      const ctx2 = this._getOrReturnCtx(input);
      addIssueToContext(ctx2, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.string,
        received: ctx2.parsedType
      });
      return INVALID;
    }
    const status = new ParseStatus();
    let ctx = void 0;
    for (const check of this._def.checks) {
      if (check.kind === "min") {
        if (input.data.length < check.value) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.too_small,
            minimum: check.value,
            type: "string",
            inclusive: true,
            exact: false,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "max") {
        if (input.data.length > check.value) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.too_big,
            maximum: check.value,
            type: "string",
            inclusive: true,
            exact: false,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "length") {
        const tooBig = input.data.length > check.value;
        const tooSmall = input.data.length < check.value;
        if (tooBig || tooSmall) {
          ctx = this._getOrReturnCtx(input, ctx);
          if (tooBig) {
            addIssueToContext(ctx, {
              code: ZodIssueCode.too_big,
              maximum: check.value,
              type: "string",
              inclusive: true,
              exact: true,
              message: check.message
            });
          } else if (tooSmall) {
            addIssueToContext(ctx, {
              code: ZodIssueCode.too_small,
              minimum: check.value,
              type: "string",
              inclusive: true,
              exact: true,
              message: check.message
            });
          }
          status.dirty();
        }
      } else if (check.kind === "email") {
        if (!emailRegex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "email",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "emoji") {
        if (!emojiRegex) {
          emojiRegex = new RegExp(_emojiRegex, "u");
        }
        if (!emojiRegex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "emoji",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "uuid") {
        if (!uuidRegex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "uuid",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "nanoid") {
        if (!nanoidRegex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "nanoid",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "cuid") {
        if (!cuidRegex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "cuid",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "cuid2") {
        if (!cuid2Regex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "cuid2",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "ulid") {
        if (!ulidRegex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "ulid",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "url") {
        try {
          new URL(input.data);
        } catch {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "url",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "regex") {
        check.regex.lastIndex = 0;
        const testResult = check.regex.test(input.data);
        if (!testResult) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "regex",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "trim") {
        input.data = input.data.trim();
      } else if (check.kind === "includes") {
        if (!input.data.includes(check.value, check.position)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.invalid_string,
            validation: { includes: check.value, position: check.position },
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "toLowerCase") {
        input.data = input.data.toLowerCase();
      } else if (check.kind === "toUpperCase") {
        input.data = input.data.toUpperCase();
      } else if (check.kind === "startsWith") {
        if (!input.data.startsWith(check.value)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.invalid_string,
            validation: { startsWith: check.value },
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "endsWith") {
        if (!input.data.endsWith(check.value)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.invalid_string,
            validation: { endsWith: check.value },
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "datetime") {
        const regex = datetimeRegex(check);
        if (!regex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.invalid_string,
            validation: "datetime",
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "date") {
        const regex = dateRegex;
        if (!regex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.invalid_string,
            validation: "date",
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "time") {
        const regex = timeRegex(check);
        if (!regex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.invalid_string,
            validation: "time",
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "duration") {
        if (!durationRegex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "duration",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "ip") {
        if (!isValidIP(input.data, check.version)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "ip",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "jwt") {
        if (!isValidJWT(input.data, check.alg)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "jwt",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "cidr") {
        if (!isValidCidr(input.data, check.version)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "cidr",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "base64") {
        if (!base64Regex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "base64",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "base64url") {
        if (!base64urlRegex.test(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            validation: "base64url",
            code: ZodIssueCode.invalid_string,
            message: check.message
          });
          status.dirty();
        }
      } else {
        util.assertNever(check);
      }
    }
    return { status: status.value, value: input.data };
  }
  _regex(regex, validation, message) {
    return this.refinement((data) => regex.test(data), {
      validation,
      code: ZodIssueCode.invalid_string,
      ...errorUtil.errToObj(message)
    });
  }
  _addCheck(check) {
    return new _ZodString({
      ...this._def,
      checks: [...this._def.checks, check]
    });
  }
  email(message) {
    return this._addCheck({ kind: "email", ...errorUtil.errToObj(message) });
  }
  url(message) {
    return this._addCheck({ kind: "url", ...errorUtil.errToObj(message) });
  }
  emoji(message) {
    return this._addCheck({ kind: "emoji", ...errorUtil.errToObj(message) });
  }
  uuid(message) {
    return this._addCheck({ kind: "uuid", ...errorUtil.errToObj(message) });
  }
  nanoid(message) {
    return this._addCheck({ kind: "nanoid", ...errorUtil.errToObj(message) });
  }
  cuid(message) {
    return this._addCheck({ kind: "cuid", ...errorUtil.errToObj(message) });
  }
  cuid2(message) {
    return this._addCheck({ kind: "cuid2", ...errorUtil.errToObj(message) });
  }
  ulid(message) {
    return this._addCheck({ kind: "ulid", ...errorUtil.errToObj(message) });
  }
  base64(message) {
    return this._addCheck({ kind: "base64", ...errorUtil.errToObj(message) });
  }
  base64url(message) {
    return this._addCheck({
      kind: "base64url",
      ...errorUtil.errToObj(message)
    });
  }
  jwt(options) {
    return this._addCheck({ kind: "jwt", ...errorUtil.errToObj(options) });
  }
  ip(options) {
    return this._addCheck({ kind: "ip", ...errorUtil.errToObj(options) });
  }
  cidr(options) {
    return this._addCheck({ kind: "cidr", ...errorUtil.errToObj(options) });
  }
  datetime(options) {
    if (typeof options === "string") {
      return this._addCheck({
        kind: "datetime",
        precision: null,
        offset: false,
        local: false,
        message: options
      });
    }
    return this._addCheck({
      kind: "datetime",
      precision: typeof options?.precision === "undefined" ? null : options?.precision,
      offset: options?.offset ?? false,
      local: options?.local ?? false,
      ...errorUtil.errToObj(options?.message)
    });
  }
  date(message) {
    return this._addCheck({ kind: "date", message });
  }
  time(options) {
    if (typeof options === "string") {
      return this._addCheck({
        kind: "time",
        precision: null,
        message: options
      });
    }
    return this._addCheck({
      kind: "time",
      precision: typeof options?.precision === "undefined" ? null : options?.precision,
      ...errorUtil.errToObj(options?.message)
    });
  }
  duration(message) {
    return this._addCheck({ kind: "duration", ...errorUtil.errToObj(message) });
  }
  regex(regex, message) {
    return this._addCheck({
      kind: "regex",
      regex,
      ...errorUtil.errToObj(message)
    });
  }
  includes(value, options) {
    return this._addCheck({
      kind: "includes",
      value,
      position: options?.position,
      ...errorUtil.errToObj(options?.message)
    });
  }
  startsWith(value, message) {
    return this._addCheck({
      kind: "startsWith",
      value,
      ...errorUtil.errToObj(message)
    });
  }
  endsWith(value, message) {
    return this._addCheck({
      kind: "endsWith",
      value,
      ...errorUtil.errToObj(message)
    });
  }
  min(minLength, message) {
    return this._addCheck({
      kind: "min",
      value: minLength,
      ...errorUtil.errToObj(message)
    });
  }
  max(maxLength, message) {
    return this._addCheck({
      kind: "max",
      value: maxLength,
      ...errorUtil.errToObj(message)
    });
  }
  length(len, message) {
    return this._addCheck({
      kind: "length",
      value: len,
      ...errorUtil.errToObj(message)
    });
  }
  /**
   * Equivalent to `.min(1)`
   */
  nonempty(message) {
    return this.min(1, errorUtil.errToObj(message));
  }
  trim() {
    return new _ZodString({
      ...this._def,
      checks: [...this._def.checks, { kind: "trim" }]
    });
  }
  toLowerCase() {
    return new _ZodString({
      ...this._def,
      checks: [...this._def.checks, { kind: "toLowerCase" }]
    });
  }
  toUpperCase() {
    return new _ZodString({
      ...this._def,
      checks: [...this._def.checks, { kind: "toUpperCase" }]
    });
  }
  get isDatetime() {
    return !!this._def.checks.find((ch) => ch.kind === "datetime");
  }
  get isDate() {
    return !!this._def.checks.find((ch) => ch.kind === "date");
  }
  get isTime() {
    return !!this._def.checks.find((ch) => ch.kind === "time");
  }
  get isDuration() {
    return !!this._def.checks.find((ch) => ch.kind === "duration");
  }
  get isEmail() {
    return !!this._def.checks.find((ch) => ch.kind === "email");
  }
  get isURL() {
    return !!this._def.checks.find((ch) => ch.kind === "url");
  }
  get isEmoji() {
    return !!this._def.checks.find((ch) => ch.kind === "emoji");
  }
  get isUUID() {
    return !!this._def.checks.find((ch) => ch.kind === "uuid");
  }
  get isNANOID() {
    return !!this._def.checks.find((ch) => ch.kind === "nanoid");
  }
  get isCUID() {
    return !!this._def.checks.find((ch) => ch.kind === "cuid");
  }
  get isCUID2() {
    return !!this._def.checks.find((ch) => ch.kind === "cuid2");
  }
  get isULID() {
    return !!this._def.checks.find((ch) => ch.kind === "ulid");
  }
  get isIP() {
    return !!this._def.checks.find((ch) => ch.kind === "ip");
  }
  get isCIDR() {
    return !!this._def.checks.find((ch) => ch.kind === "cidr");
  }
  get isBase64() {
    return !!this._def.checks.find((ch) => ch.kind === "base64");
  }
  get isBase64url() {
    return !!this._def.checks.find((ch) => ch.kind === "base64url");
  }
  get minLength() {
    let min = null;
    for (const ch of this._def.checks) {
      if (ch.kind === "min") {
        if (min === null || ch.value > min)
          min = ch.value;
      }
    }
    return min;
  }
  get maxLength() {
    let max = null;
    for (const ch of this._def.checks) {
      if (ch.kind === "max") {
        if (max === null || ch.value < max)
          max = ch.value;
      }
    }
    return max;
  }
};
ZodString.create = (params) => {
  return new ZodString({
    checks: [],
    typeName: ZodFirstPartyTypeKind.ZodString,
    coerce: params?.coerce ?? false,
    ...processCreateParams(params)
  });
};
function floatSafeRemainder(val, step) {
  const valDecCount = (val.toString().split(".")[1] || "").length;
  const stepDecCount = (step.toString().split(".")[1] || "").length;
  const decCount = valDecCount > stepDecCount ? valDecCount : stepDecCount;
  const valInt = Number.parseInt(val.toFixed(decCount).replace(".", ""));
  const stepInt = Number.parseInt(step.toFixed(decCount).replace(".", ""));
  return valInt % stepInt / 10 ** decCount;
}
var ZodNumber = class _ZodNumber extends ZodType {
  constructor() {
    super(...arguments);
    this.min = this.gte;
    this.max = this.lte;
    this.step = this.multipleOf;
  }
  _parse(input) {
    if (this._def.coerce) {
      input.data = Number(input.data);
    }
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.number) {
      const ctx2 = this._getOrReturnCtx(input);
      addIssueToContext(ctx2, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.number,
        received: ctx2.parsedType
      });
      return INVALID;
    }
    let ctx = void 0;
    const status = new ParseStatus();
    for (const check of this._def.checks) {
      if (check.kind === "int") {
        if (!util.isInteger(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.invalid_type,
            expected: "integer",
            received: "float",
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "min") {
        const tooSmall = check.inclusive ? input.data < check.value : input.data <= check.value;
        if (tooSmall) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.too_small,
            minimum: check.value,
            type: "number",
            inclusive: check.inclusive,
            exact: false,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "max") {
        const tooBig = check.inclusive ? input.data > check.value : input.data >= check.value;
        if (tooBig) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.too_big,
            maximum: check.value,
            type: "number",
            inclusive: check.inclusive,
            exact: false,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "multipleOf") {
        if (floatSafeRemainder(input.data, check.value) !== 0) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.not_multiple_of,
            multipleOf: check.value,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "finite") {
        if (!Number.isFinite(input.data)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.not_finite,
            message: check.message
          });
          status.dirty();
        }
      } else {
        util.assertNever(check);
      }
    }
    return { status: status.value, value: input.data };
  }
  gte(value, message) {
    return this.setLimit("min", value, true, errorUtil.toString(message));
  }
  gt(value, message) {
    return this.setLimit("min", value, false, errorUtil.toString(message));
  }
  lte(value, message) {
    return this.setLimit("max", value, true, errorUtil.toString(message));
  }
  lt(value, message) {
    return this.setLimit("max", value, false, errorUtil.toString(message));
  }
  setLimit(kind, value, inclusive, message) {
    return new _ZodNumber({
      ...this._def,
      checks: [
        ...this._def.checks,
        {
          kind,
          value,
          inclusive,
          message: errorUtil.toString(message)
        }
      ]
    });
  }
  _addCheck(check) {
    return new _ZodNumber({
      ...this._def,
      checks: [...this._def.checks, check]
    });
  }
  int(message) {
    return this._addCheck({
      kind: "int",
      message: errorUtil.toString(message)
    });
  }
  positive(message) {
    return this._addCheck({
      kind: "min",
      value: 0,
      inclusive: false,
      message: errorUtil.toString(message)
    });
  }
  negative(message) {
    return this._addCheck({
      kind: "max",
      value: 0,
      inclusive: false,
      message: errorUtil.toString(message)
    });
  }
  nonpositive(message) {
    return this._addCheck({
      kind: "max",
      value: 0,
      inclusive: true,
      message: errorUtil.toString(message)
    });
  }
  nonnegative(message) {
    return this._addCheck({
      kind: "min",
      value: 0,
      inclusive: true,
      message: errorUtil.toString(message)
    });
  }
  multipleOf(value, message) {
    return this._addCheck({
      kind: "multipleOf",
      value,
      message: errorUtil.toString(message)
    });
  }
  finite(message) {
    return this._addCheck({
      kind: "finite",
      message: errorUtil.toString(message)
    });
  }
  safe(message) {
    return this._addCheck({
      kind: "min",
      inclusive: true,
      value: Number.MIN_SAFE_INTEGER,
      message: errorUtil.toString(message)
    })._addCheck({
      kind: "max",
      inclusive: true,
      value: Number.MAX_SAFE_INTEGER,
      message: errorUtil.toString(message)
    });
  }
  get minValue() {
    let min = null;
    for (const ch of this._def.checks) {
      if (ch.kind === "min") {
        if (min === null || ch.value > min)
          min = ch.value;
      }
    }
    return min;
  }
  get maxValue() {
    let max = null;
    for (const ch of this._def.checks) {
      if (ch.kind === "max") {
        if (max === null || ch.value < max)
          max = ch.value;
      }
    }
    return max;
  }
  get isInt() {
    return !!this._def.checks.find((ch) => ch.kind === "int" || ch.kind === "multipleOf" && util.isInteger(ch.value));
  }
  get isFinite() {
    let max = null;
    let min = null;
    for (const ch of this._def.checks) {
      if (ch.kind === "finite" || ch.kind === "int" || ch.kind === "multipleOf") {
        return true;
      } else if (ch.kind === "min") {
        if (min === null || ch.value > min)
          min = ch.value;
      } else if (ch.kind === "max") {
        if (max === null || ch.value < max)
          max = ch.value;
      }
    }
    return Number.isFinite(min) && Number.isFinite(max);
  }
};
ZodNumber.create = (params) => {
  return new ZodNumber({
    checks: [],
    typeName: ZodFirstPartyTypeKind.ZodNumber,
    coerce: params?.coerce || false,
    ...processCreateParams(params)
  });
};
var ZodBigInt = class _ZodBigInt extends ZodType {
  constructor() {
    super(...arguments);
    this.min = this.gte;
    this.max = this.lte;
  }
  _parse(input) {
    if (this._def.coerce) {
      try {
        input.data = BigInt(input.data);
      } catch {
        return this._getInvalidInput(input);
      }
    }
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.bigint) {
      return this._getInvalidInput(input);
    }
    let ctx = void 0;
    const status = new ParseStatus();
    for (const check of this._def.checks) {
      if (check.kind === "min") {
        const tooSmall = check.inclusive ? input.data < check.value : input.data <= check.value;
        if (tooSmall) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.too_small,
            type: "bigint",
            minimum: check.value,
            inclusive: check.inclusive,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "max") {
        const tooBig = check.inclusive ? input.data > check.value : input.data >= check.value;
        if (tooBig) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.too_big,
            type: "bigint",
            maximum: check.value,
            inclusive: check.inclusive,
            message: check.message
          });
          status.dirty();
        }
      } else if (check.kind === "multipleOf") {
        if (input.data % check.value !== BigInt(0)) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.not_multiple_of,
            multipleOf: check.value,
            message: check.message
          });
          status.dirty();
        }
      } else {
        util.assertNever(check);
      }
    }
    return { status: status.value, value: input.data };
  }
  _getInvalidInput(input) {
    const ctx = this._getOrReturnCtx(input);
    addIssueToContext(ctx, {
      code: ZodIssueCode.invalid_type,
      expected: ZodParsedType.bigint,
      received: ctx.parsedType
    });
    return INVALID;
  }
  gte(value, message) {
    return this.setLimit("min", value, true, errorUtil.toString(message));
  }
  gt(value, message) {
    return this.setLimit("min", value, false, errorUtil.toString(message));
  }
  lte(value, message) {
    return this.setLimit("max", value, true, errorUtil.toString(message));
  }
  lt(value, message) {
    return this.setLimit("max", value, false, errorUtil.toString(message));
  }
  setLimit(kind, value, inclusive, message) {
    return new _ZodBigInt({
      ...this._def,
      checks: [
        ...this._def.checks,
        {
          kind,
          value,
          inclusive,
          message: errorUtil.toString(message)
        }
      ]
    });
  }
  _addCheck(check) {
    return new _ZodBigInt({
      ...this._def,
      checks: [...this._def.checks, check]
    });
  }
  positive(message) {
    return this._addCheck({
      kind: "min",
      value: BigInt(0),
      inclusive: false,
      message: errorUtil.toString(message)
    });
  }
  negative(message) {
    return this._addCheck({
      kind: "max",
      value: BigInt(0),
      inclusive: false,
      message: errorUtil.toString(message)
    });
  }
  nonpositive(message) {
    return this._addCheck({
      kind: "max",
      value: BigInt(0),
      inclusive: true,
      message: errorUtil.toString(message)
    });
  }
  nonnegative(message) {
    return this._addCheck({
      kind: "min",
      value: BigInt(0),
      inclusive: true,
      message: errorUtil.toString(message)
    });
  }
  multipleOf(value, message) {
    return this._addCheck({
      kind: "multipleOf",
      value,
      message: errorUtil.toString(message)
    });
  }
  get minValue() {
    let min = null;
    for (const ch of this._def.checks) {
      if (ch.kind === "min") {
        if (min === null || ch.value > min)
          min = ch.value;
      }
    }
    return min;
  }
  get maxValue() {
    let max = null;
    for (const ch of this._def.checks) {
      if (ch.kind === "max") {
        if (max === null || ch.value < max)
          max = ch.value;
      }
    }
    return max;
  }
};
ZodBigInt.create = (params) => {
  return new ZodBigInt({
    checks: [],
    typeName: ZodFirstPartyTypeKind.ZodBigInt,
    coerce: params?.coerce ?? false,
    ...processCreateParams(params)
  });
};
var ZodBoolean = class extends ZodType {
  _parse(input) {
    if (this._def.coerce) {
      input.data = Boolean(input.data);
    }
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.boolean) {
      const ctx = this._getOrReturnCtx(input);
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.boolean,
        received: ctx.parsedType
      });
      return INVALID;
    }
    return OK(input.data);
  }
};
ZodBoolean.create = (params) => {
  return new ZodBoolean({
    typeName: ZodFirstPartyTypeKind.ZodBoolean,
    coerce: params?.coerce || false,
    ...processCreateParams(params)
  });
};
var ZodDate = class _ZodDate extends ZodType {
  _parse(input) {
    if (this._def.coerce) {
      input.data = new Date(input.data);
    }
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.date) {
      const ctx2 = this._getOrReturnCtx(input);
      addIssueToContext(ctx2, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.date,
        received: ctx2.parsedType
      });
      return INVALID;
    }
    if (Number.isNaN(input.data.getTime())) {
      const ctx2 = this._getOrReturnCtx(input);
      addIssueToContext(ctx2, {
        code: ZodIssueCode.invalid_date
      });
      return INVALID;
    }
    const status = new ParseStatus();
    let ctx = void 0;
    for (const check of this._def.checks) {
      if (check.kind === "min") {
        if (input.data.getTime() < check.value) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.too_small,
            message: check.message,
            inclusive: true,
            exact: false,
            minimum: check.value,
            type: "date"
          });
          status.dirty();
        }
      } else if (check.kind === "max") {
        if (input.data.getTime() > check.value) {
          ctx = this._getOrReturnCtx(input, ctx);
          addIssueToContext(ctx, {
            code: ZodIssueCode.too_big,
            message: check.message,
            inclusive: true,
            exact: false,
            maximum: check.value,
            type: "date"
          });
          status.dirty();
        }
      } else {
        util.assertNever(check);
      }
    }
    return {
      status: status.value,
      value: new Date(input.data.getTime())
    };
  }
  _addCheck(check) {
    return new _ZodDate({
      ...this._def,
      checks: [...this._def.checks, check]
    });
  }
  min(minDate, message) {
    return this._addCheck({
      kind: "min",
      value: minDate.getTime(),
      message: errorUtil.toString(message)
    });
  }
  max(maxDate, message) {
    return this._addCheck({
      kind: "max",
      value: maxDate.getTime(),
      message: errorUtil.toString(message)
    });
  }
  get minDate() {
    let min = null;
    for (const ch of this._def.checks) {
      if (ch.kind === "min") {
        if (min === null || ch.value > min)
          min = ch.value;
      }
    }
    return min != null ? new Date(min) : null;
  }
  get maxDate() {
    let max = null;
    for (const ch of this._def.checks) {
      if (ch.kind === "max") {
        if (max === null || ch.value < max)
          max = ch.value;
      }
    }
    return max != null ? new Date(max) : null;
  }
};
ZodDate.create = (params) => {
  return new ZodDate({
    checks: [],
    coerce: params?.coerce || false,
    typeName: ZodFirstPartyTypeKind.ZodDate,
    ...processCreateParams(params)
  });
};
var ZodSymbol = class extends ZodType {
  _parse(input) {
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.symbol) {
      const ctx = this._getOrReturnCtx(input);
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.symbol,
        received: ctx.parsedType
      });
      return INVALID;
    }
    return OK(input.data);
  }
};
ZodSymbol.create = (params) => {
  return new ZodSymbol({
    typeName: ZodFirstPartyTypeKind.ZodSymbol,
    ...processCreateParams(params)
  });
};
var ZodUndefined = class extends ZodType {
  _parse(input) {
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.undefined) {
      const ctx = this._getOrReturnCtx(input);
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.undefined,
        received: ctx.parsedType
      });
      return INVALID;
    }
    return OK(input.data);
  }
};
ZodUndefined.create = (params) => {
  return new ZodUndefined({
    typeName: ZodFirstPartyTypeKind.ZodUndefined,
    ...processCreateParams(params)
  });
};
var ZodNull = class extends ZodType {
  _parse(input) {
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.null) {
      const ctx = this._getOrReturnCtx(input);
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.null,
        received: ctx.parsedType
      });
      return INVALID;
    }
    return OK(input.data);
  }
};
ZodNull.create = (params) => {
  return new ZodNull({
    typeName: ZodFirstPartyTypeKind.ZodNull,
    ...processCreateParams(params)
  });
};
var ZodAny = class extends ZodType {
  constructor() {
    super(...arguments);
    this._any = true;
  }
  _parse(input) {
    return OK(input.data);
  }
};
ZodAny.create = (params) => {
  return new ZodAny({
    typeName: ZodFirstPartyTypeKind.ZodAny,
    ...processCreateParams(params)
  });
};
var ZodUnknown = class extends ZodType {
  constructor() {
    super(...arguments);
    this._unknown = true;
  }
  _parse(input) {
    return OK(input.data);
  }
};
ZodUnknown.create = (params) => {
  return new ZodUnknown({
    typeName: ZodFirstPartyTypeKind.ZodUnknown,
    ...processCreateParams(params)
  });
};
var ZodNever = class extends ZodType {
  _parse(input) {
    const ctx = this._getOrReturnCtx(input);
    addIssueToContext(ctx, {
      code: ZodIssueCode.invalid_type,
      expected: ZodParsedType.never,
      received: ctx.parsedType
    });
    return INVALID;
  }
};
ZodNever.create = (params) => {
  return new ZodNever({
    typeName: ZodFirstPartyTypeKind.ZodNever,
    ...processCreateParams(params)
  });
};
var ZodVoid = class extends ZodType {
  _parse(input) {
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.undefined) {
      const ctx = this._getOrReturnCtx(input);
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.void,
        received: ctx.parsedType
      });
      return INVALID;
    }
    return OK(input.data);
  }
};
ZodVoid.create = (params) => {
  return new ZodVoid({
    typeName: ZodFirstPartyTypeKind.ZodVoid,
    ...processCreateParams(params)
  });
};
var ZodArray = class _ZodArray extends ZodType {
  _parse(input) {
    const { ctx, status } = this._processInputParams(input);
    const def = this._def;
    if (ctx.parsedType !== ZodParsedType.array) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.array,
        received: ctx.parsedType
      });
      return INVALID;
    }
    if (def.exactLength !== null) {
      const tooBig = ctx.data.length > def.exactLength.value;
      const tooSmall = ctx.data.length < def.exactLength.value;
      if (tooBig || tooSmall) {
        addIssueToContext(ctx, {
          code: tooBig ? ZodIssueCode.too_big : ZodIssueCode.too_small,
          minimum: tooSmall ? def.exactLength.value : void 0,
          maximum: tooBig ? def.exactLength.value : void 0,
          type: "array",
          inclusive: true,
          exact: true,
          message: def.exactLength.message
        });
        status.dirty();
      }
    }
    if (def.minLength !== null) {
      if (ctx.data.length < def.minLength.value) {
        addIssueToContext(ctx, {
          code: ZodIssueCode.too_small,
          minimum: def.minLength.value,
          type: "array",
          inclusive: true,
          exact: false,
          message: def.minLength.message
        });
        status.dirty();
      }
    }
    if (def.maxLength !== null) {
      if (ctx.data.length > def.maxLength.value) {
        addIssueToContext(ctx, {
          code: ZodIssueCode.too_big,
          maximum: def.maxLength.value,
          type: "array",
          inclusive: true,
          exact: false,
          message: def.maxLength.message
        });
        status.dirty();
      }
    }
    if (ctx.common.async) {
      return Promise.all([...ctx.data].map((item, i) => {
        return def.type._parseAsync(new ParseInputLazyPath(ctx, item, ctx.path, i));
      })).then((result2) => {
        return ParseStatus.mergeArray(status, result2);
      });
    }
    const result = [...ctx.data].map((item, i) => {
      return def.type._parseSync(new ParseInputLazyPath(ctx, item, ctx.path, i));
    });
    return ParseStatus.mergeArray(status, result);
  }
  get element() {
    return this._def.type;
  }
  min(minLength, message) {
    return new _ZodArray({
      ...this._def,
      minLength: { value: minLength, message: errorUtil.toString(message) }
    });
  }
  max(maxLength, message) {
    return new _ZodArray({
      ...this._def,
      maxLength: { value: maxLength, message: errorUtil.toString(message) }
    });
  }
  length(len, message) {
    return new _ZodArray({
      ...this._def,
      exactLength: { value: len, message: errorUtil.toString(message) }
    });
  }
  nonempty(message) {
    return this.min(1, message);
  }
};
ZodArray.create = (schema, params) => {
  return new ZodArray({
    type: schema,
    minLength: null,
    maxLength: null,
    exactLength: null,
    typeName: ZodFirstPartyTypeKind.ZodArray,
    ...processCreateParams(params)
  });
};
function deepPartialify(schema) {
  if (schema instanceof ZodObject) {
    const newShape = {};
    for (const key in schema.shape) {
      const fieldSchema = schema.shape[key];
      newShape[key] = ZodOptional.create(deepPartialify(fieldSchema));
    }
    return new ZodObject({
      ...schema._def,
      shape: () => newShape
    });
  } else if (schema instanceof ZodArray) {
    return new ZodArray({
      ...schema._def,
      type: deepPartialify(schema.element)
    });
  } else if (schema instanceof ZodOptional) {
    return ZodOptional.create(deepPartialify(schema.unwrap()));
  } else if (schema instanceof ZodNullable) {
    return ZodNullable.create(deepPartialify(schema.unwrap()));
  } else if (schema instanceof ZodTuple) {
    return ZodTuple.create(schema.items.map((item) => deepPartialify(item)));
  } else {
    return schema;
  }
}
var ZodObject = class _ZodObject extends ZodType {
  constructor() {
    super(...arguments);
    this._cached = null;
    this.nonstrict = this.passthrough;
    this.augment = this.extend;
  }
  _getCached() {
    if (this._cached !== null)
      return this._cached;
    const shape = this._def.shape();
    const keys = util.objectKeys(shape);
    this._cached = { shape, keys };
    return this._cached;
  }
  _parse(input) {
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.object) {
      const ctx2 = this._getOrReturnCtx(input);
      addIssueToContext(ctx2, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.object,
        received: ctx2.parsedType
      });
      return INVALID;
    }
    const { status, ctx } = this._processInputParams(input);
    const { shape, keys: shapeKeys } = this._getCached();
    const extraKeys = [];
    if (!(this._def.catchall instanceof ZodNever && this._def.unknownKeys === "strip")) {
      for (const key in ctx.data) {
        if (!shapeKeys.includes(key)) {
          extraKeys.push(key);
        }
      }
    }
    const pairs = [];
    for (const key of shapeKeys) {
      const keyValidator = shape[key];
      const value = ctx.data[key];
      pairs.push({
        key: { status: "valid", value: key },
        value: keyValidator._parse(new ParseInputLazyPath(ctx, value, ctx.path, key)),
        alwaysSet: key in ctx.data
      });
    }
    if (this._def.catchall instanceof ZodNever) {
      const unknownKeys = this._def.unknownKeys;
      if (unknownKeys === "passthrough") {
        for (const key of extraKeys) {
          pairs.push({
            key: { status: "valid", value: key },
            value: { status: "valid", value: ctx.data[key] }
          });
        }
      } else if (unknownKeys === "strict") {
        if (extraKeys.length > 0) {
          addIssueToContext(ctx, {
            code: ZodIssueCode.unrecognized_keys,
            keys: extraKeys
          });
          status.dirty();
        }
      } else if (unknownKeys === "strip") {
      } else {
        throw new Error(`Internal ZodObject error: invalid unknownKeys value.`);
      }
    } else {
      const catchall = this._def.catchall;
      for (const key of extraKeys) {
        const value = ctx.data[key];
        pairs.push({
          key: { status: "valid", value: key },
          value: catchall._parse(
            new ParseInputLazyPath(ctx, value, ctx.path, key)
            //, ctx.child(key), value, getParsedType(value)
          ),
          alwaysSet: key in ctx.data
        });
      }
    }
    if (ctx.common.async) {
      return Promise.resolve().then(async () => {
        const syncPairs = [];
        for (const pair of pairs) {
          const key = await pair.key;
          const value = await pair.value;
          syncPairs.push({
            key,
            value,
            alwaysSet: pair.alwaysSet
          });
        }
        return syncPairs;
      }).then((syncPairs) => {
        return ParseStatus.mergeObjectSync(status, syncPairs);
      });
    } else {
      return ParseStatus.mergeObjectSync(status, pairs);
    }
  }
  get shape() {
    return this._def.shape();
  }
  strict(message) {
    errorUtil.errToObj;
    return new _ZodObject({
      ...this._def,
      unknownKeys: "strict",
      ...message !== void 0 ? {
        errorMap: (issue, ctx) => {
          const defaultError = this._def.errorMap?.(issue, ctx).message ?? ctx.defaultError;
          if (issue.code === "unrecognized_keys")
            return {
              message: errorUtil.errToObj(message).message ?? defaultError
            };
          return {
            message: defaultError
          };
        }
      } : {}
    });
  }
  strip() {
    return new _ZodObject({
      ...this._def,
      unknownKeys: "strip"
    });
  }
  passthrough() {
    return new _ZodObject({
      ...this._def,
      unknownKeys: "passthrough"
    });
  }
  // const AugmentFactory =
  //   <Def extends ZodObjectDef>(def: Def) =>
  //   <Augmentation extends ZodRawShape>(
  //     augmentation: Augmentation
  //   ): ZodObject<
  //     extendShape<ReturnType<Def["shape"]>, Augmentation>,
  //     Def["unknownKeys"],
  //     Def["catchall"]
  //   > => {
  //     return new ZodObject({
  //       ...def,
  //       shape: () => ({
  //         ...def.shape(),
  //         ...augmentation,
  //       }),
  //     }) as any;
  //   };
  extend(augmentation) {
    return new _ZodObject({
      ...this._def,
      shape: () => ({
        ...this._def.shape(),
        ...augmentation
      })
    });
  }
  /**
   * Prior to zod@1.0.12 there was a bug in the
   * inferred type of merged objects. Please
   * upgrade if you are experiencing issues.
   */
  merge(merging) {
    const merged = new _ZodObject({
      unknownKeys: merging._def.unknownKeys,
      catchall: merging._def.catchall,
      shape: () => ({
        ...this._def.shape(),
        ...merging._def.shape()
      }),
      typeName: ZodFirstPartyTypeKind.ZodObject
    });
    return merged;
  }
  // merge<
  //   Incoming extends AnyZodObject,
  //   Augmentation extends Incoming["shape"],
  //   NewOutput extends {
  //     [k in keyof Augmentation | keyof Output]: k extends keyof Augmentation
  //       ? Augmentation[k]["_output"]
  //       : k extends keyof Output
  //       ? Output[k]
  //       : never;
  //   },
  //   NewInput extends {
  //     [k in keyof Augmentation | keyof Input]: k extends keyof Augmentation
  //       ? Augmentation[k]["_input"]
  //       : k extends keyof Input
  //       ? Input[k]
  //       : never;
  //   }
  // >(
  //   merging: Incoming
  // ): ZodObject<
  //   extendShape<T, ReturnType<Incoming["_def"]["shape"]>>,
  //   Incoming["_def"]["unknownKeys"],
  //   Incoming["_def"]["catchall"],
  //   NewOutput,
  //   NewInput
  // > {
  //   const merged: any = new ZodObject({
  //     unknownKeys: merging._def.unknownKeys,
  //     catchall: merging._def.catchall,
  //     shape: () =>
  //       objectUtil.mergeShapes(this._def.shape(), merging._def.shape()),
  //     typeName: ZodFirstPartyTypeKind.ZodObject,
  //   }) as any;
  //   return merged;
  // }
  setKey(key, schema) {
    return this.augment({ [key]: schema });
  }
  // merge<Incoming extends AnyZodObject>(
  //   merging: Incoming
  // ): //ZodObject<T & Incoming["_shape"], UnknownKeys, Catchall> = (merging) => {
  // ZodObject<
  //   extendShape<T, ReturnType<Incoming["_def"]["shape"]>>,
  //   Incoming["_def"]["unknownKeys"],
  //   Incoming["_def"]["catchall"]
  // > {
  //   // const mergedShape = objectUtil.mergeShapes(
  //   //   this._def.shape(),
  //   //   merging._def.shape()
  //   // );
  //   const merged: any = new ZodObject({
  //     unknownKeys: merging._def.unknownKeys,
  //     catchall: merging._def.catchall,
  //     shape: () =>
  //       objectUtil.mergeShapes(this._def.shape(), merging._def.shape()),
  //     typeName: ZodFirstPartyTypeKind.ZodObject,
  //   }) as any;
  //   return merged;
  // }
  catchall(index) {
    return new _ZodObject({
      ...this._def,
      catchall: index
    });
  }
  pick(mask) {
    const shape = {};
    for (const key of util.objectKeys(mask)) {
      if (mask[key] && this.shape[key]) {
        shape[key] = this.shape[key];
      }
    }
    return new _ZodObject({
      ...this._def,
      shape: () => shape
    });
  }
  omit(mask) {
    const shape = {};
    for (const key of util.objectKeys(this.shape)) {
      if (!mask[key]) {
        shape[key] = this.shape[key];
      }
    }
    return new _ZodObject({
      ...this._def,
      shape: () => shape
    });
  }
  /**
   * @deprecated
   */
  deepPartial() {
    return deepPartialify(this);
  }
  partial(mask) {
    const newShape = {};
    for (const key of util.objectKeys(this.shape)) {
      const fieldSchema = this.shape[key];
      if (mask && !mask[key]) {
        newShape[key] = fieldSchema;
      } else {
        newShape[key] = fieldSchema.optional();
      }
    }
    return new _ZodObject({
      ...this._def,
      shape: () => newShape
    });
  }
  required(mask) {
    const newShape = {};
    for (const key of util.objectKeys(this.shape)) {
      if (mask && !mask[key]) {
        newShape[key] = this.shape[key];
      } else {
        const fieldSchema = this.shape[key];
        let newField = fieldSchema;
        while (newField instanceof ZodOptional) {
          newField = newField._def.innerType;
        }
        newShape[key] = newField;
      }
    }
    return new _ZodObject({
      ...this._def,
      shape: () => newShape
    });
  }
  keyof() {
    return createZodEnum(util.objectKeys(this.shape));
  }
};
ZodObject.create = (shape, params) => {
  return new ZodObject({
    shape: () => shape,
    unknownKeys: "strip",
    catchall: ZodNever.create(),
    typeName: ZodFirstPartyTypeKind.ZodObject,
    ...processCreateParams(params)
  });
};
ZodObject.strictCreate = (shape, params) => {
  return new ZodObject({
    shape: () => shape,
    unknownKeys: "strict",
    catchall: ZodNever.create(),
    typeName: ZodFirstPartyTypeKind.ZodObject,
    ...processCreateParams(params)
  });
};
ZodObject.lazycreate = (shape, params) => {
  return new ZodObject({
    shape,
    unknownKeys: "strip",
    catchall: ZodNever.create(),
    typeName: ZodFirstPartyTypeKind.ZodObject,
    ...processCreateParams(params)
  });
};
var ZodUnion = class extends ZodType {
  _parse(input) {
    const { ctx } = this._processInputParams(input);
    const options = this._def.options;
    function handleResults(results) {
      for (const result of results) {
        if (result.result.status === "valid") {
          return result.result;
        }
      }
      for (const result of results) {
        if (result.result.status === "dirty") {
          ctx.common.issues.push(...result.ctx.common.issues);
          return result.result;
        }
      }
      const unionErrors = results.map((result) => new ZodError(result.ctx.common.issues));
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_union,
        unionErrors
      });
      return INVALID;
    }
    if (ctx.common.async) {
      return Promise.all(options.map(async (option) => {
        const childCtx = {
          ...ctx,
          common: {
            ...ctx.common,
            issues: []
          },
          parent: null
        };
        return {
          result: await option._parseAsync({
            data: ctx.data,
            path: ctx.path,
            parent: childCtx
          }),
          ctx: childCtx
        };
      })).then(handleResults);
    } else {
      let dirty = void 0;
      const issues = [];
      for (const option of options) {
        const childCtx = {
          ...ctx,
          common: {
            ...ctx.common,
            issues: []
          },
          parent: null
        };
        const result = option._parseSync({
          data: ctx.data,
          path: ctx.path,
          parent: childCtx
        });
        if (result.status === "valid") {
          return result;
        } else if (result.status === "dirty" && !dirty) {
          dirty = { result, ctx: childCtx };
        }
        if (childCtx.common.issues.length) {
          issues.push(childCtx.common.issues);
        }
      }
      if (dirty) {
        ctx.common.issues.push(...dirty.ctx.common.issues);
        return dirty.result;
      }
      const unionErrors = issues.map((issues2) => new ZodError(issues2));
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_union,
        unionErrors
      });
      return INVALID;
    }
  }
  get options() {
    return this._def.options;
  }
};
ZodUnion.create = (types, params) => {
  return new ZodUnion({
    options: types,
    typeName: ZodFirstPartyTypeKind.ZodUnion,
    ...processCreateParams(params)
  });
};
var getDiscriminator = (type) => {
  if (type instanceof ZodLazy) {
    return getDiscriminator(type.schema);
  } else if (type instanceof ZodEffects) {
    return getDiscriminator(type.innerType());
  } else if (type instanceof ZodLiteral) {
    return [type.value];
  } else if (type instanceof ZodEnum) {
    return type.options;
  } else if (type instanceof ZodNativeEnum) {
    return util.objectValues(type.enum);
  } else if (type instanceof ZodDefault) {
    return getDiscriminator(type._def.innerType);
  } else if (type instanceof ZodUndefined) {
    return [void 0];
  } else if (type instanceof ZodNull) {
    return [null];
  } else if (type instanceof ZodOptional) {
    return [void 0, ...getDiscriminator(type.unwrap())];
  } else if (type instanceof ZodNullable) {
    return [null, ...getDiscriminator(type.unwrap())];
  } else if (type instanceof ZodBranded) {
    return getDiscriminator(type.unwrap());
  } else if (type instanceof ZodReadonly) {
    return getDiscriminator(type.unwrap());
  } else if (type instanceof ZodCatch) {
    return getDiscriminator(type._def.innerType);
  } else {
    return [];
  }
};
var ZodDiscriminatedUnion = class _ZodDiscriminatedUnion extends ZodType {
  _parse(input) {
    const { ctx } = this._processInputParams(input);
    if (ctx.parsedType !== ZodParsedType.object) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.object,
        received: ctx.parsedType
      });
      return INVALID;
    }
    const discriminator = this.discriminator;
    const discriminatorValue = ctx.data[discriminator];
    const option = this.optionsMap.get(discriminatorValue);
    if (!option) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_union_discriminator,
        options: Array.from(this.optionsMap.keys()),
        path: [discriminator]
      });
      return INVALID;
    }
    if (ctx.common.async) {
      return option._parseAsync({
        data: ctx.data,
        path: ctx.path,
        parent: ctx
      });
    } else {
      return option._parseSync({
        data: ctx.data,
        path: ctx.path,
        parent: ctx
      });
    }
  }
  get discriminator() {
    return this._def.discriminator;
  }
  get options() {
    return this._def.options;
  }
  get optionsMap() {
    return this._def.optionsMap;
  }
  /**
   * The constructor of the discriminated union schema. Its behaviour is very similar to that of the normal z.union() constructor.
   * However, it only allows a union of objects, all of which need to share a discriminator property. This property must
   * have a different value for each object in the union.
   * @param discriminator the name of the discriminator property
   * @param types an array of object schemas
   * @param params
   */
  static create(discriminator, options, params) {
    const optionsMap = /* @__PURE__ */ new Map();
    for (const type of options) {
      const discriminatorValues = getDiscriminator(type.shape[discriminator]);
      if (!discriminatorValues.length) {
        throw new Error(`A discriminator value for key \`${discriminator}\` could not be extracted from all schema options`);
      }
      for (const value of discriminatorValues) {
        if (optionsMap.has(value)) {
          throw new Error(`Discriminator property ${String(discriminator)} has duplicate value ${String(value)}`);
        }
        optionsMap.set(value, type);
      }
    }
    return new _ZodDiscriminatedUnion({
      typeName: ZodFirstPartyTypeKind.ZodDiscriminatedUnion,
      discriminator,
      options,
      optionsMap,
      ...processCreateParams(params)
    });
  }
};
function mergeValues(a, b) {
  const aType = getParsedType(a);
  const bType = getParsedType(b);
  if (a === b) {
    return { valid: true, data: a };
  } else if (aType === ZodParsedType.object && bType === ZodParsedType.object) {
    const bKeys = util.objectKeys(b);
    const sharedKeys = util.objectKeys(a).filter((key) => bKeys.indexOf(key) !== -1);
    const newObj = { ...a, ...b };
    for (const key of sharedKeys) {
      const sharedValue = mergeValues(a[key], b[key]);
      if (!sharedValue.valid) {
        return { valid: false };
      }
      newObj[key] = sharedValue.data;
    }
    return { valid: true, data: newObj };
  } else if (aType === ZodParsedType.array && bType === ZodParsedType.array) {
    if (a.length !== b.length) {
      return { valid: false };
    }
    const newArray = [];
    for (let index = 0; index < a.length; index++) {
      const itemA = a[index];
      const itemB = b[index];
      const sharedValue = mergeValues(itemA, itemB);
      if (!sharedValue.valid) {
        return { valid: false };
      }
      newArray.push(sharedValue.data);
    }
    return { valid: true, data: newArray };
  } else if (aType === ZodParsedType.date && bType === ZodParsedType.date && +a === +b) {
    return { valid: true, data: a };
  } else {
    return { valid: false };
  }
}
var ZodIntersection = class extends ZodType {
  _parse(input) {
    const { status, ctx } = this._processInputParams(input);
    const handleParsed = (parsedLeft, parsedRight) => {
      if (isAborted(parsedLeft) || isAborted(parsedRight)) {
        return INVALID;
      }
      const merged = mergeValues(parsedLeft.value, parsedRight.value);
      if (!merged.valid) {
        addIssueToContext(ctx, {
          code: ZodIssueCode.invalid_intersection_types
        });
        return INVALID;
      }
      if (isDirty(parsedLeft) || isDirty(parsedRight)) {
        status.dirty();
      }
      return { status: status.value, value: merged.data };
    };
    if (ctx.common.async) {
      return Promise.all([
        this._def.left._parseAsync({
          data: ctx.data,
          path: ctx.path,
          parent: ctx
        }),
        this._def.right._parseAsync({
          data: ctx.data,
          path: ctx.path,
          parent: ctx
        })
      ]).then(([left, right]) => handleParsed(left, right));
    } else {
      return handleParsed(this._def.left._parseSync({
        data: ctx.data,
        path: ctx.path,
        parent: ctx
      }), this._def.right._parseSync({
        data: ctx.data,
        path: ctx.path,
        parent: ctx
      }));
    }
  }
};
ZodIntersection.create = (left, right, params) => {
  return new ZodIntersection({
    left,
    right,
    typeName: ZodFirstPartyTypeKind.ZodIntersection,
    ...processCreateParams(params)
  });
};
var ZodTuple = class _ZodTuple extends ZodType {
  _parse(input) {
    const { status, ctx } = this._processInputParams(input);
    if (ctx.parsedType !== ZodParsedType.array) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.array,
        received: ctx.parsedType
      });
      return INVALID;
    }
    if (ctx.data.length < this._def.items.length) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.too_small,
        minimum: this._def.items.length,
        inclusive: true,
        exact: false,
        type: "array"
      });
      return INVALID;
    }
    const rest = this._def.rest;
    if (!rest && ctx.data.length > this._def.items.length) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.too_big,
        maximum: this._def.items.length,
        inclusive: true,
        exact: false,
        type: "array"
      });
      status.dirty();
    }
    const items = [...ctx.data].map((item, itemIndex) => {
      const schema = this._def.items[itemIndex] || this._def.rest;
      if (!schema)
        return null;
      return schema._parse(new ParseInputLazyPath(ctx, item, ctx.path, itemIndex));
    }).filter((x) => !!x);
    if (ctx.common.async) {
      return Promise.all(items).then((results) => {
        return ParseStatus.mergeArray(status, results);
      });
    } else {
      return ParseStatus.mergeArray(status, items);
    }
  }
  get items() {
    return this._def.items;
  }
  rest(rest) {
    return new _ZodTuple({
      ...this._def,
      rest
    });
  }
};
ZodTuple.create = (schemas, params) => {
  if (!Array.isArray(schemas)) {
    throw new Error("You must pass an array of schemas to z.tuple([ ... ])");
  }
  return new ZodTuple({
    items: schemas,
    typeName: ZodFirstPartyTypeKind.ZodTuple,
    rest: null,
    ...processCreateParams(params)
  });
};
var ZodRecord = class _ZodRecord extends ZodType {
  get keySchema() {
    return this._def.keyType;
  }
  get valueSchema() {
    return this._def.valueType;
  }
  _parse(input) {
    const { status, ctx } = this._processInputParams(input);
    if (ctx.parsedType !== ZodParsedType.object) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.object,
        received: ctx.parsedType
      });
      return INVALID;
    }
    const pairs = [];
    const keyType = this._def.keyType;
    const valueType = this._def.valueType;
    for (const key in ctx.data) {
      pairs.push({
        key: keyType._parse(new ParseInputLazyPath(ctx, key, ctx.path, key)),
        value: valueType._parse(new ParseInputLazyPath(ctx, ctx.data[key], ctx.path, key)),
        alwaysSet: key in ctx.data
      });
    }
    if (ctx.common.async) {
      return ParseStatus.mergeObjectAsync(status, pairs);
    } else {
      return ParseStatus.mergeObjectSync(status, pairs);
    }
  }
  get element() {
    return this._def.valueType;
  }
  static create(first, second, third) {
    if (second instanceof ZodType) {
      return new _ZodRecord({
        keyType: first,
        valueType: second,
        typeName: ZodFirstPartyTypeKind.ZodRecord,
        ...processCreateParams(third)
      });
    }
    return new _ZodRecord({
      keyType: ZodString.create(),
      valueType: first,
      typeName: ZodFirstPartyTypeKind.ZodRecord,
      ...processCreateParams(second)
    });
  }
};
var ZodMap = class extends ZodType {
  get keySchema() {
    return this._def.keyType;
  }
  get valueSchema() {
    return this._def.valueType;
  }
  _parse(input) {
    const { status, ctx } = this._processInputParams(input);
    if (ctx.parsedType !== ZodParsedType.map) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.map,
        received: ctx.parsedType
      });
      return INVALID;
    }
    const keyType = this._def.keyType;
    const valueType = this._def.valueType;
    const pairs = [...ctx.data.entries()].map(([key, value], index) => {
      return {
        key: keyType._parse(new ParseInputLazyPath(ctx, key, ctx.path, [index, "key"])),
        value: valueType._parse(new ParseInputLazyPath(ctx, value, ctx.path, [index, "value"]))
      };
    });
    if (ctx.common.async) {
      const finalMap = /* @__PURE__ */ new Map();
      return Promise.resolve().then(async () => {
        for (const pair of pairs) {
          const key = await pair.key;
          const value = await pair.value;
          if (key.status === "aborted" || value.status === "aborted") {
            return INVALID;
          }
          if (key.status === "dirty" || value.status === "dirty") {
            status.dirty();
          }
          finalMap.set(key.value, value.value);
        }
        return { status: status.value, value: finalMap };
      });
    } else {
      const finalMap = /* @__PURE__ */ new Map();
      for (const pair of pairs) {
        const key = pair.key;
        const value = pair.value;
        if (key.status === "aborted" || value.status === "aborted") {
          return INVALID;
        }
        if (key.status === "dirty" || value.status === "dirty") {
          status.dirty();
        }
        finalMap.set(key.value, value.value);
      }
      return { status: status.value, value: finalMap };
    }
  }
};
ZodMap.create = (keyType, valueType, params) => {
  return new ZodMap({
    valueType,
    keyType,
    typeName: ZodFirstPartyTypeKind.ZodMap,
    ...processCreateParams(params)
  });
};
var ZodSet = class _ZodSet extends ZodType {
  _parse(input) {
    const { status, ctx } = this._processInputParams(input);
    if (ctx.parsedType !== ZodParsedType.set) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.set,
        received: ctx.parsedType
      });
      return INVALID;
    }
    const def = this._def;
    if (def.minSize !== null) {
      if (ctx.data.size < def.minSize.value) {
        addIssueToContext(ctx, {
          code: ZodIssueCode.too_small,
          minimum: def.minSize.value,
          type: "set",
          inclusive: true,
          exact: false,
          message: def.minSize.message
        });
        status.dirty();
      }
    }
    if (def.maxSize !== null) {
      if (ctx.data.size > def.maxSize.value) {
        addIssueToContext(ctx, {
          code: ZodIssueCode.too_big,
          maximum: def.maxSize.value,
          type: "set",
          inclusive: true,
          exact: false,
          message: def.maxSize.message
        });
        status.dirty();
      }
    }
    const valueType = this._def.valueType;
    function finalizeSet(elements2) {
      const parsedSet = /* @__PURE__ */ new Set();
      for (const element of elements2) {
        if (element.status === "aborted")
          return INVALID;
        if (element.status === "dirty")
          status.dirty();
        parsedSet.add(element.value);
      }
      return { status: status.value, value: parsedSet };
    }
    const elements = [...ctx.data.values()].map((item, i) => valueType._parse(new ParseInputLazyPath(ctx, item, ctx.path, i)));
    if (ctx.common.async) {
      return Promise.all(elements).then((elements2) => finalizeSet(elements2));
    } else {
      return finalizeSet(elements);
    }
  }
  min(minSize, message) {
    return new _ZodSet({
      ...this._def,
      minSize: { value: minSize, message: errorUtil.toString(message) }
    });
  }
  max(maxSize, message) {
    return new _ZodSet({
      ...this._def,
      maxSize: { value: maxSize, message: errorUtil.toString(message) }
    });
  }
  size(size, message) {
    return this.min(size, message).max(size, message);
  }
  nonempty(message) {
    return this.min(1, message);
  }
};
ZodSet.create = (valueType, params) => {
  return new ZodSet({
    valueType,
    minSize: null,
    maxSize: null,
    typeName: ZodFirstPartyTypeKind.ZodSet,
    ...processCreateParams(params)
  });
};
var ZodFunction = class _ZodFunction extends ZodType {
  constructor() {
    super(...arguments);
    this.validate = this.implement;
  }
  _parse(input) {
    const { ctx } = this._processInputParams(input);
    if (ctx.parsedType !== ZodParsedType.function) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.function,
        received: ctx.parsedType
      });
      return INVALID;
    }
    function makeArgsIssue(args, error) {
      return makeIssue({
        data: args,
        path: ctx.path,
        errorMaps: [ctx.common.contextualErrorMap, ctx.schemaErrorMap, getErrorMap(), en_default].filter((x) => !!x),
        issueData: {
          code: ZodIssueCode.invalid_arguments,
          argumentsError: error
        }
      });
    }
    function makeReturnsIssue(returns, error) {
      return makeIssue({
        data: returns,
        path: ctx.path,
        errorMaps: [ctx.common.contextualErrorMap, ctx.schemaErrorMap, getErrorMap(), en_default].filter((x) => !!x),
        issueData: {
          code: ZodIssueCode.invalid_return_type,
          returnTypeError: error
        }
      });
    }
    const params = { errorMap: ctx.common.contextualErrorMap };
    const fn = ctx.data;
    if (this._def.returns instanceof ZodPromise) {
      const me = this;
      return OK(async function(...args) {
        const error = new ZodError([]);
        const parsedArgs = await me._def.args.parseAsync(args, params).catch((e) => {
          error.addIssue(makeArgsIssue(args, e));
          throw error;
        });
        const result = await Reflect.apply(fn, this, parsedArgs);
        const parsedReturns = await me._def.returns._def.type.parseAsync(result, params).catch((e) => {
          error.addIssue(makeReturnsIssue(result, e));
          throw error;
        });
        return parsedReturns;
      });
    } else {
      const me = this;
      return OK(function(...args) {
        const parsedArgs = me._def.args.safeParse(args, params);
        if (!parsedArgs.success) {
          throw new ZodError([makeArgsIssue(args, parsedArgs.error)]);
        }
        const result = Reflect.apply(fn, this, parsedArgs.data);
        const parsedReturns = me._def.returns.safeParse(result, params);
        if (!parsedReturns.success) {
          throw new ZodError([makeReturnsIssue(result, parsedReturns.error)]);
        }
        return parsedReturns.data;
      });
    }
  }
  parameters() {
    return this._def.args;
  }
  returnType() {
    return this._def.returns;
  }
  args(...items) {
    return new _ZodFunction({
      ...this._def,
      args: ZodTuple.create(items).rest(ZodUnknown.create())
    });
  }
  returns(returnType) {
    return new _ZodFunction({
      ...this._def,
      returns: returnType
    });
  }
  implement(func) {
    const validatedFunc = this.parse(func);
    return validatedFunc;
  }
  strictImplement(func) {
    const validatedFunc = this.parse(func);
    return validatedFunc;
  }
  static create(args, returns, params) {
    return new _ZodFunction({
      args: args ? args : ZodTuple.create([]).rest(ZodUnknown.create()),
      returns: returns || ZodUnknown.create(),
      typeName: ZodFirstPartyTypeKind.ZodFunction,
      ...processCreateParams(params)
    });
  }
};
var ZodLazy = class extends ZodType {
  get schema() {
    return this._def.getter();
  }
  _parse(input) {
    const { ctx } = this._processInputParams(input);
    const lazySchema = this._def.getter();
    return lazySchema._parse({ data: ctx.data, path: ctx.path, parent: ctx });
  }
};
ZodLazy.create = (getter, params) => {
  return new ZodLazy({
    getter,
    typeName: ZodFirstPartyTypeKind.ZodLazy,
    ...processCreateParams(params)
  });
};
var ZodLiteral = class extends ZodType {
  _parse(input) {
    if (input.data !== this._def.value) {
      const ctx = this._getOrReturnCtx(input);
      addIssueToContext(ctx, {
        received: ctx.data,
        code: ZodIssueCode.invalid_literal,
        expected: this._def.value
      });
      return INVALID;
    }
    return { status: "valid", value: input.data };
  }
  get value() {
    return this._def.value;
  }
};
ZodLiteral.create = (value, params) => {
  return new ZodLiteral({
    value,
    typeName: ZodFirstPartyTypeKind.ZodLiteral,
    ...processCreateParams(params)
  });
};
function createZodEnum(values, params) {
  return new ZodEnum({
    values,
    typeName: ZodFirstPartyTypeKind.ZodEnum,
    ...processCreateParams(params)
  });
}
var ZodEnum = class _ZodEnum extends ZodType {
  _parse(input) {
    if (typeof input.data !== "string") {
      const ctx = this._getOrReturnCtx(input);
      const expectedValues = this._def.values;
      addIssueToContext(ctx, {
        expected: util.joinValues(expectedValues),
        received: ctx.parsedType,
        code: ZodIssueCode.invalid_type
      });
      return INVALID;
    }
    if (!this._cache) {
      this._cache = new Set(this._def.values);
    }
    if (!this._cache.has(input.data)) {
      const ctx = this._getOrReturnCtx(input);
      const expectedValues = this._def.values;
      addIssueToContext(ctx, {
        received: ctx.data,
        code: ZodIssueCode.invalid_enum_value,
        options: expectedValues
      });
      return INVALID;
    }
    return OK(input.data);
  }
  get options() {
    return this._def.values;
  }
  get enum() {
    const enumValues = {};
    for (const val of this._def.values) {
      enumValues[val] = val;
    }
    return enumValues;
  }
  get Values() {
    const enumValues = {};
    for (const val of this._def.values) {
      enumValues[val] = val;
    }
    return enumValues;
  }
  get Enum() {
    const enumValues = {};
    for (const val of this._def.values) {
      enumValues[val] = val;
    }
    return enumValues;
  }
  extract(values, newDef = this._def) {
    return _ZodEnum.create(values, {
      ...this._def,
      ...newDef
    });
  }
  exclude(values, newDef = this._def) {
    return _ZodEnum.create(this.options.filter((opt) => !values.includes(opt)), {
      ...this._def,
      ...newDef
    });
  }
};
ZodEnum.create = createZodEnum;
var ZodNativeEnum = class extends ZodType {
  _parse(input) {
    const nativeEnumValues = util.getValidEnumValues(this._def.values);
    const ctx = this._getOrReturnCtx(input);
    if (ctx.parsedType !== ZodParsedType.string && ctx.parsedType !== ZodParsedType.number) {
      const expectedValues = util.objectValues(nativeEnumValues);
      addIssueToContext(ctx, {
        expected: util.joinValues(expectedValues),
        received: ctx.parsedType,
        code: ZodIssueCode.invalid_type
      });
      return INVALID;
    }
    if (!this._cache) {
      this._cache = new Set(util.getValidEnumValues(this._def.values));
    }
    if (!this._cache.has(input.data)) {
      const expectedValues = util.objectValues(nativeEnumValues);
      addIssueToContext(ctx, {
        received: ctx.data,
        code: ZodIssueCode.invalid_enum_value,
        options: expectedValues
      });
      return INVALID;
    }
    return OK(input.data);
  }
  get enum() {
    return this._def.values;
  }
};
ZodNativeEnum.create = (values, params) => {
  return new ZodNativeEnum({
    values,
    typeName: ZodFirstPartyTypeKind.ZodNativeEnum,
    ...processCreateParams(params)
  });
};
var ZodPromise = class extends ZodType {
  unwrap() {
    return this._def.type;
  }
  _parse(input) {
    const { ctx } = this._processInputParams(input);
    if (ctx.parsedType !== ZodParsedType.promise && ctx.common.async === false) {
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.promise,
        received: ctx.parsedType
      });
      return INVALID;
    }
    const promisified = ctx.parsedType === ZodParsedType.promise ? ctx.data : Promise.resolve(ctx.data);
    return OK(promisified.then((data) => {
      return this._def.type.parseAsync(data, {
        path: ctx.path,
        errorMap: ctx.common.contextualErrorMap
      });
    }));
  }
};
ZodPromise.create = (schema, params) => {
  return new ZodPromise({
    type: schema,
    typeName: ZodFirstPartyTypeKind.ZodPromise,
    ...processCreateParams(params)
  });
};
var ZodEffects = class extends ZodType {
  innerType() {
    return this._def.schema;
  }
  sourceType() {
    return this._def.schema._def.typeName === ZodFirstPartyTypeKind.ZodEffects ? this._def.schema.sourceType() : this._def.schema;
  }
  _parse(input) {
    const { status, ctx } = this._processInputParams(input);
    const effect = this._def.effect || null;
    const checkCtx = {
      addIssue: (arg) => {
        addIssueToContext(ctx, arg);
        if (arg.fatal) {
          status.abort();
        } else {
          status.dirty();
        }
      },
      get path() {
        return ctx.path;
      }
    };
    checkCtx.addIssue = checkCtx.addIssue.bind(checkCtx);
    if (effect.type === "preprocess") {
      const processed = effect.transform(ctx.data, checkCtx);
      if (ctx.common.async) {
        return Promise.resolve(processed).then(async (processed2) => {
          if (status.value === "aborted")
            return INVALID;
          const result = await this._def.schema._parseAsync({
            data: processed2,
            path: ctx.path,
            parent: ctx
          });
          if (result.status === "aborted")
            return INVALID;
          if (result.status === "dirty")
            return DIRTY(result.value);
          if (status.value === "dirty")
            return DIRTY(result.value);
          return result;
        });
      } else {
        if (status.value === "aborted")
          return INVALID;
        const result = this._def.schema._parseSync({
          data: processed,
          path: ctx.path,
          parent: ctx
        });
        if (result.status === "aborted")
          return INVALID;
        if (result.status === "dirty")
          return DIRTY(result.value);
        if (status.value === "dirty")
          return DIRTY(result.value);
        return result;
      }
    }
    if (effect.type === "refinement") {
      const executeRefinement = (acc) => {
        const result = effect.refinement(acc, checkCtx);
        if (ctx.common.async) {
          return Promise.resolve(result);
        }
        if (result instanceof Promise) {
          throw new Error("Async refinement encountered during synchronous parse operation. Use .parseAsync instead.");
        }
        return acc;
      };
      if (ctx.common.async === false) {
        const inner = this._def.schema._parseSync({
          data: ctx.data,
          path: ctx.path,
          parent: ctx
        });
        if (inner.status === "aborted")
          return INVALID;
        if (inner.status === "dirty")
          status.dirty();
        executeRefinement(inner.value);
        return { status: status.value, value: inner.value };
      } else {
        return this._def.schema._parseAsync({ data: ctx.data, path: ctx.path, parent: ctx }).then((inner) => {
          if (inner.status === "aborted")
            return INVALID;
          if (inner.status === "dirty")
            status.dirty();
          return executeRefinement(inner.value).then(() => {
            return { status: status.value, value: inner.value };
          });
        });
      }
    }
    if (effect.type === "transform") {
      if (ctx.common.async === false) {
        const base = this._def.schema._parseSync({
          data: ctx.data,
          path: ctx.path,
          parent: ctx
        });
        if (!isValid(base))
          return INVALID;
        const result = effect.transform(base.value, checkCtx);
        if (result instanceof Promise) {
          throw new Error(`Asynchronous transform encountered during synchronous parse operation. Use .parseAsync instead.`);
        }
        return { status: status.value, value: result };
      } else {
        return this._def.schema._parseAsync({ data: ctx.data, path: ctx.path, parent: ctx }).then((base) => {
          if (!isValid(base))
            return INVALID;
          return Promise.resolve(effect.transform(base.value, checkCtx)).then((result) => ({
            status: status.value,
            value: result
          }));
        });
      }
    }
    util.assertNever(effect);
  }
};
ZodEffects.create = (schema, effect, params) => {
  return new ZodEffects({
    schema,
    typeName: ZodFirstPartyTypeKind.ZodEffects,
    effect,
    ...processCreateParams(params)
  });
};
ZodEffects.createWithPreprocess = (preprocess, schema, params) => {
  return new ZodEffects({
    schema,
    effect: { type: "preprocess", transform: preprocess },
    typeName: ZodFirstPartyTypeKind.ZodEffects,
    ...processCreateParams(params)
  });
};
var ZodOptional = class extends ZodType {
  _parse(input) {
    const parsedType = this._getType(input);
    if (parsedType === ZodParsedType.undefined) {
      return OK(void 0);
    }
    return this._def.innerType._parse(input);
  }
  unwrap() {
    return this._def.innerType;
  }
};
ZodOptional.create = (type, params) => {
  return new ZodOptional({
    innerType: type,
    typeName: ZodFirstPartyTypeKind.ZodOptional,
    ...processCreateParams(params)
  });
};
var ZodNullable = class extends ZodType {
  _parse(input) {
    const parsedType = this._getType(input);
    if (parsedType === ZodParsedType.null) {
      return OK(null);
    }
    return this._def.innerType._parse(input);
  }
  unwrap() {
    return this._def.innerType;
  }
};
ZodNullable.create = (type, params) => {
  return new ZodNullable({
    innerType: type,
    typeName: ZodFirstPartyTypeKind.ZodNullable,
    ...processCreateParams(params)
  });
};
var ZodDefault = class extends ZodType {
  _parse(input) {
    const { ctx } = this._processInputParams(input);
    let data = ctx.data;
    if (ctx.parsedType === ZodParsedType.undefined) {
      data = this._def.defaultValue();
    }
    return this._def.innerType._parse({
      data,
      path: ctx.path,
      parent: ctx
    });
  }
  removeDefault() {
    return this._def.innerType;
  }
};
ZodDefault.create = (type, params) => {
  return new ZodDefault({
    innerType: type,
    typeName: ZodFirstPartyTypeKind.ZodDefault,
    defaultValue: typeof params.default === "function" ? params.default : () => params.default,
    ...processCreateParams(params)
  });
};
var ZodCatch = class extends ZodType {
  _parse(input) {
    const { ctx } = this._processInputParams(input);
    const newCtx = {
      ...ctx,
      common: {
        ...ctx.common,
        issues: []
      }
    };
    const result = this._def.innerType._parse({
      data: newCtx.data,
      path: newCtx.path,
      parent: {
        ...newCtx
      }
    });
    if (isAsync(result)) {
      return result.then((result2) => {
        return {
          status: "valid",
          value: result2.status === "valid" ? result2.value : this._def.catchValue({
            get error() {
              return new ZodError(newCtx.common.issues);
            },
            input: newCtx.data
          })
        };
      });
    } else {
      return {
        status: "valid",
        value: result.status === "valid" ? result.value : this._def.catchValue({
          get error() {
            return new ZodError(newCtx.common.issues);
          },
          input: newCtx.data
        })
      };
    }
  }
  removeCatch() {
    return this._def.innerType;
  }
};
ZodCatch.create = (type, params) => {
  return new ZodCatch({
    innerType: type,
    typeName: ZodFirstPartyTypeKind.ZodCatch,
    catchValue: typeof params.catch === "function" ? params.catch : () => params.catch,
    ...processCreateParams(params)
  });
};
var ZodNaN = class extends ZodType {
  _parse(input) {
    const parsedType = this._getType(input);
    if (parsedType !== ZodParsedType.nan) {
      const ctx = this._getOrReturnCtx(input);
      addIssueToContext(ctx, {
        code: ZodIssueCode.invalid_type,
        expected: ZodParsedType.nan,
        received: ctx.parsedType
      });
      return INVALID;
    }
    return { status: "valid", value: input.data };
  }
};
ZodNaN.create = (params) => {
  return new ZodNaN({
    typeName: ZodFirstPartyTypeKind.ZodNaN,
    ...processCreateParams(params)
  });
};
var BRAND = Symbol("zod_brand");
var ZodBranded = class extends ZodType {
  _parse(input) {
    const { ctx } = this._processInputParams(input);
    const data = ctx.data;
    return this._def.type._parse({
      data,
      path: ctx.path,
      parent: ctx
    });
  }
  unwrap() {
    return this._def.type;
  }
};
var ZodPipeline = class _ZodPipeline extends ZodType {
  _parse(input) {
    const { status, ctx } = this._processInputParams(input);
    if (ctx.common.async) {
      const handleAsync = async () => {
        const inResult = await this._def.in._parseAsync({
          data: ctx.data,
          path: ctx.path,
          parent: ctx
        });
        if (inResult.status === "aborted")
          return INVALID;
        if (inResult.status === "dirty") {
          status.dirty();
          return DIRTY(inResult.value);
        } else {
          return this._def.out._parseAsync({
            data: inResult.value,
            path: ctx.path,
            parent: ctx
          });
        }
      };
      return handleAsync();
    } else {
      const inResult = this._def.in._parseSync({
        data: ctx.data,
        path: ctx.path,
        parent: ctx
      });
      if (inResult.status === "aborted")
        return INVALID;
      if (inResult.status === "dirty") {
        status.dirty();
        return {
          status: "dirty",
          value: inResult.value
        };
      } else {
        return this._def.out._parseSync({
          data: inResult.value,
          path: ctx.path,
          parent: ctx
        });
      }
    }
  }
  static create(a, b) {
    return new _ZodPipeline({
      in: a,
      out: b,
      typeName: ZodFirstPartyTypeKind.ZodPipeline
    });
  }
};
var ZodReadonly = class extends ZodType {
  _parse(input) {
    const result = this._def.innerType._parse(input);
    const freeze = (data) => {
      if (isValid(data)) {
        data.value = Object.freeze(data.value);
      }
      return data;
    };
    return isAsync(result) ? result.then((data) => freeze(data)) : freeze(result);
  }
  unwrap() {
    return this._def.innerType;
  }
};
ZodReadonly.create = (type, params) => {
  return new ZodReadonly({
    innerType: type,
    typeName: ZodFirstPartyTypeKind.ZodReadonly,
    ...processCreateParams(params)
  });
};
function cleanParams(params, data) {
  const p = typeof params === "function" ? params(data) : typeof params === "string" ? { message: params } : params;
  const p2 = typeof p === "string" ? { message: p } : p;
  return p2;
}
function custom(check, _params = {}, fatal) {
  if (check)
    return ZodAny.create().superRefine((data, ctx) => {
      const r = check(data);
      if (r instanceof Promise) {
        return r.then((r2) => {
          if (!r2) {
            const params = cleanParams(_params, data);
            const _fatal = params.fatal ?? fatal ?? true;
            ctx.addIssue({ code: "custom", ...params, fatal: _fatal });
          }
        });
      }
      if (!r) {
        const params = cleanParams(_params, data);
        const _fatal = params.fatal ?? fatal ?? true;
        ctx.addIssue({ code: "custom", ...params, fatal: _fatal });
      }
      return;
    });
  return ZodAny.create();
}
var late = {
  object: ZodObject.lazycreate
};
var ZodFirstPartyTypeKind;
(function(ZodFirstPartyTypeKind2) {
  ZodFirstPartyTypeKind2["ZodString"] = "ZodString";
  ZodFirstPartyTypeKind2["ZodNumber"] = "ZodNumber";
  ZodFirstPartyTypeKind2["ZodNaN"] = "ZodNaN";
  ZodFirstPartyTypeKind2["ZodBigInt"] = "ZodBigInt";
  ZodFirstPartyTypeKind2["ZodBoolean"] = "ZodBoolean";
  ZodFirstPartyTypeKind2["ZodDate"] = "ZodDate";
  ZodFirstPartyTypeKind2["ZodSymbol"] = "ZodSymbol";
  ZodFirstPartyTypeKind2["ZodUndefined"] = "ZodUndefined";
  ZodFirstPartyTypeKind2["ZodNull"] = "ZodNull";
  ZodFirstPartyTypeKind2["ZodAny"] = "ZodAny";
  ZodFirstPartyTypeKind2["ZodUnknown"] = "ZodUnknown";
  ZodFirstPartyTypeKind2["ZodNever"] = "ZodNever";
  ZodFirstPartyTypeKind2["ZodVoid"] = "ZodVoid";
  ZodFirstPartyTypeKind2["ZodArray"] = "ZodArray";
  ZodFirstPartyTypeKind2["ZodObject"] = "ZodObject";
  ZodFirstPartyTypeKind2["ZodUnion"] = "ZodUnion";
  ZodFirstPartyTypeKind2["ZodDiscriminatedUnion"] = "ZodDiscriminatedUnion";
  ZodFirstPartyTypeKind2["ZodIntersection"] = "ZodIntersection";
  ZodFirstPartyTypeKind2["ZodTuple"] = "ZodTuple";
  ZodFirstPartyTypeKind2["ZodRecord"] = "ZodRecord";
  ZodFirstPartyTypeKind2["ZodMap"] = "ZodMap";
  ZodFirstPartyTypeKind2["ZodSet"] = "ZodSet";
  ZodFirstPartyTypeKind2["ZodFunction"] = "ZodFunction";
  ZodFirstPartyTypeKind2["ZodLazy"] = "ZodLazy";
  ZodFirstPartyTypeKind2["ZodLiteral"] = "ZodLiteral";
  ZodFirstPartyTypeKind2["ZodEnum"] = "ZodEnum";
  ZodFirstPartyTypeKind2["ZodEffects"] = "ZodEffects";
  ZodFirstPartyTypeKind2["ZodNativeEnum"] = "ZodNativeEnum";
  ZodFirstPartyTypeKind2["ZodOptional"] = "ZodOptional";
  ZodFirstPartyTypeKind2["ZodNullable"] = "ZodNullable";
  ZodFirstPartyTypeKind2["ZodDefault"] = "ZodDefault";
  ZodFirstPartyTypeKind2["ZodCatch"] = "ZodCatch";
  ZodFirstPartyTypeKind2["ZodPromise"] = "ZodPromise";
  ZodFirstPartyTypeKind2["ZodBranded"] = "ZodBranded";
  ZodFirstPartyTypeKind2["ZodPipeline"] = "ZodPipeline";
  ZodFirstPartyTypeKind2["ZodReadonly"] = "ZodReadonly";
})(ZodFirstPartyTypeKind || (ZodFirstPartyTypeKind = {}));
var instanceOfType = (cls, params = {
  message: `Input not instance of ${cls.name}`
}) => custom((data) => data instanceof cls, params);
var stringType = ZodString.create;
var numberType = ZodNumber.create;
var nanType = ZodNaN.create;
var bigIntType = ZodBigInt.create;
var booleanType = ZodBoolean.create;
var dateType = ZodDate.create;
var symbolType = ZodSymbol.create;
var undefinedType = ZodUndefined.create;
var nullType = ZodNull.create;
var anyType = ZodAny.create;
var unknownType = ZodUnknown.create;
var neverType = ZodNever.create;
var voidType = ZodVoid.create;
var arrayType = ZodArray.create;
var objectType = ZodObject.create;
var strictObjectType = ZodObject.strictCreate;
var unionType = ZodUnion.create;
var discriminatedUnionType = ZodDiscriminatedUnion.create;
var intersectionType = ZodIntersection.create;
var tupleType = ZodTuple.create;
var recordType = ZodRecord.create;
var mapType = ZodMap.create;
var setType = ZodSet.create;
var functionType = ZodFunction.create;
var lazyType = ZodLazy.create;
var literalType = ZodLiteral.create;
var enumType = ZodEnum.create;
var nativeEnumType = ZodNativeEnum.create;
var promiseType = ZodPromise.create;
var effectsType = ZodEffects.create;
var optionalType = ZodOptional.create;
var nullableType = ZodNullable.create;
var preprocessType = ZodEffects.createWithPreprocess;
var pipelineType = ZodPipeline.create;
var ostring = () => stringType().optional();
var onumber = () => numberType().optional();
var oboolean = () => booleanType().optional();
var coerce = {
  string: (arg) => ZodString.create({ ...arg, coerce: true }),
  number: (arg) => ZodNumber.create({ ...arg, coerce: true }),
  boolean: (arg) => ZodBoolean.create({
    ...arg,
    coerce: true
  }),
  bigint: (arg) => ZodBigInt.create({ ...arg, coerce: true }),
  date: (arg) => ZodDate.create({ ...arg, coerce: true })
};
var NEVER = INVALID;

// node_modules/@modelcontextprotocol/sdk/dist/esm/types.js
var LATEST_PROTOCOL_VERSION = "2025-03-26";
var SUPPORTED_PROTOCOL_VERSIONS = [
  LATEST_PROTOCOL_VERSION,
  "2024-11-05",
  "2024-10-07"
];
var JSONRPC_VERSION = "2.0";
var ProgressTokenSchema = external_exports.union([external_exports.string(), external_exports.number().int()]);
var CursorSchema = external_exports.string();
var RequestMetaSchema = external_exports.object({
  /**
   * If specified, the caller is requesting out-of-band progress notifications for this request (as represented by notifications/progress). The value of this parameter is an opaque token that will be attached to any subsequent notifications. The receiver is not obligated to provide these notifications.
   */
  progressToken: external_exports.optional(ProgressTokenSchema)
}).passthrough();
var BaseRequestParamsSchema = external_exports.object({
  _meta: external_exports.optional(RequestMetaSchema)
}).passthrough();
var RequestSchema = external_exports.object({
  method: external_exports.string(),
  params: external_exports.optional(BaseRequestParamsSchema)
});
var BaseNotificationParamsSchema = external_exports.object({
  /**
   * This parameter name is reserved by MCP to allow clients and servers to attach additional metadata to their notifications.
   */
  _meta: external_exports.optional(external_exports.object({}).passthrough())
}).passthrough();
var NotificationSchema = external_exports.object({
  method: external_exports.string(),
  params: external_exports.optional(BaseNotificationParamsSchema)
});
var ResultSchema = external_exports.object({
  /**
   * This result property is reserved by the protocol to allow clients and servers to attach additional metadata to their responses.
   */
  _meta: external_exports.optional(external_exports.object({}).passthrough())
}).passthrough();
var RequestIdSchema = external_exports.union([external_exports.string(), external_exports.number().int()]);
var JSONRPCRequestSchema = external_exports.object({
  jsonrpc: external_exports.literal(JSONRPC_VERSION),
  id: RequestIdSchema
}).merge(RequestSchema).strict();
var isJSONRPCRequest = (value) => JSONRPCRequestSchema.safeParse(value).success;
var JSONRPCNotificationSchema = external_exports.object({
  jsonrpc: external_exports.literal(JSONRPC_VERSION)
}).merge(NotificationSchema).strict();
var isJSONRPCNotification = (value) => JSONRPCNotificationSchema.safeParse(value).success;
var JSONRPCResponseSchema = external_exports.object({
  jsonrpc: external_exports.literal(JSONRPC_VERSION),
  id: RequestIdSchema,
  result: ResultSchema
}).strict();
var isJSONRPCResponse = (value) => JSONRPCResponseSchema.safeParse(value).success;
var ErrorCode;
(function(ErrorCode2) {
  ErrorCode2[ErrorCode2["ConnectionClosed"] = -32e3] = "ConnectionClosed";
  ErrorCode2[ErrorCode2["RequestTimeout"] = -32001] = "RequestTimeout";
  ErrorCode2[ErrorCode2["ParseError"] = -32700] = "ParseError";
  ErrorCode2[ErrorCode2["InvalidRequest"] = -32600] = "InvalidRequest";
  ErrorCode2[ErrorCode2["MethodNotFound"] = -32601] = "MethodNotFound";
  ErrorCode2[ErrorCode2["InvalidParams"] = -32602] = "InvalidParams";
  ErrorCode2[ErrorCode2["InternalError"] = -32603] = "InternalError";
})(ErrorCode || (ErrorCode = {}));
var JSONRPCErrorSchema = external_exports.object({
  jsonrpc: external_exports.literal(JSONRPC_VERSION),
  id: RequestIdSchema,
  error: external_exports.object({
    /**
     * The error type that occurred.
     */
    code: external_exports.number().int(),
    /**
     * A short description of the error. The message SHOULD be limited to a concise single sentence.
     */
    message: external_exports.string(),
    /**
     * Additional information about the error. The value of this member is defined by the sender (e.g. detailed error information, nested errors etc.).
     */
    data: external_exports.optional(external_exports.unknown())
  })
}).strict();
var isJSONRPCError = (value) => JSONRPCErrorSchema.safeParse(value).success;
var JSONRPCMessageSchema = external_exports.union([
  JSONRPCRequestSchema,
  JSONRPCNotificationSchema,
  JSONRPCResponseSchema,
  JSONRPCErrorSchema
]);
var EmptyResultSchema = ResultSchema.strict();
var CancelledNotificationSchema = NotificationSchema.extend({
  method: external_exports.literal("notifications/cancelled"),
  params: BaseNotificationParamsSchema.extend({
    /**
     * The ID of the request to cancel.
     *
     * This MUST correspond to the ID of a request previously issued in the same direction.
     */
    requestId: RequestIdSchema,
    /**
     * An optional string describing the reason for the cancellation. This MAY be logged or presented to the user.
     */
    reason: external_exports.string().optional()
  })
});
var ImplementationSchema = external_exports.object({
  name: external_exports.string(),
  version: external_exports.string()
}).passthrough();
var ClientCapabilitiesSchema = external_exports.object({
  /**
   * Experimental, non-standard capabilities that the client supports.
   */
  experimental: external_exports.optional(external_exports.object({}).passthrough()),
  /**
   * Present if the client supports sampling from an LLM.
   */
  sampling: external_exports.optional(external_exports.object({}).passthrough()),
  /**
   * Present if the client supports listing roots.
   */
  roots: external_exports.optional(external_exports.object({
    /**
     * Whether the client supports issuing notifications for changes to the roots list.
     */
    listChanged: external_exports.optional(external_exports.boolean())
  }).passthrough())
}).passthrough();
var InitializeRequestSchema = RequestSchema.extend({
  method: external_exports.literal("initialize"),
  params: BaseRequestParamsSchema.extend({
    /**
     * The latest version of the Model Context Protocol that the client supports. The client MAY decide to support older versions as well.
     */
    protocolVersion: external_exports.string(),
    capabilities: ClientCapabilitiesSchema,
    clientInfo: ImplementationSchema
  })
});
var ServerCapabilitiesSchema = external_exports.object({
  /**
   * Experimental, non-standard capabilities that the server supports.
   */
  experimental: external_exports.optional(external_exports.object({}).passthrough()),
  /**
   * Present if the server supports sending log messages to the client.
   */
  logging: external_exports.optional(external_exports.object({}).passthrough()),
  /**
   * Present if the server supports sending completions to the client.
   */
  completions: external_exports.optional(external_exports.object({}).passthrough()),
  /**
   * Present if the server offers any prompt templates.
   */
  prompts: external_exports.optional(external_exports.object({
    /**
     * Whether this server supports issuing notifications for changes to the prompt list.
     */
    listChanged: external_exports.optional(external_exports.boolean())
  }).passthrough()),
  /**
   * Present if the server offers any resources to read.
   */
  resources: external_exports.optional(external_exports.object({
    /**
     * Whether this server supports clients subscribing to resource updates.
     */
    subscribe: external_exports.optional(external_exports.boolean()),
    /**
     * Whether this server supports issuing notifications for changes to the resource list.
     */
    listChanged: external_exports.optional(external_exports.boolean())
  }).passthrough()),
  /**
   * Present if the server offers any tools to call.
   */
  tools: external_exports.optional(external_exports.object({
    /**
     * Whether this server supports issuing notifications for changes to the tool list.
     */
    listChanged: external_exports.optional(external_exports.boolean())
  }).passthrough())
}).passthrough();
var InitializeResultSchema = ResultSchema.extend({
  /**
   * The version of the Model Context Protocol that the server wants to use. This may not match the version that the client requested. If the client cannot support this version, it MUST disconnect.
   */
  protocolVersion: external_exports.string(),
  capabilities: ServerCapabilitiesSchema,
  serverInfo: ImplementationSchema,
  /**
   * Instructions describing how to use the server and its features.
   *
   * This can be used by clients to improve the LLM's understanding of available tools, resources, etc. It can be thought of like a "hint" to the model. For example, this information MAY be added to the system prompt.
   */
  instructions: external_exports.optional(external_exports.string())
});
var InitializedNotificationSchema = NotificationSchema.extend({
  method: external_exports.literal("notifications/initialized")
});
var PingRequestSchema = RequestSchema.extend({
  method: external_exports.literal("ping")
});
var ProgressSchema = external_exports.object({
  /**
   * The progress thus far. This should increase every time progress is made, even if the total is unknown.
   */
  progress: external_exports.number(),
  /**
   * Total number of items to process (or total progress required), if known.
   */
  total: external_exports.optional(external_exports.number()),
  /**
   * An optional message describing the current progress.
   */
  message: external_exports.optional(external_exports.string())
}).passthrough();
var ProgressNotificationSchema = NotificationSchema.extend({
  method: external_exports.literal("notifications/progress"),
  params: BaseNotificationParamsSchema.merge(ProgressSchema).extend({
    /**
     * The progress token which was given in the initial request, used to associate this notification with the request that is proceeding.
     */
    progressToken: ProgressTokenSchema
  })
});
var PaginatedRequestSchema = RequestSchema.extend({
  params: BaseRequestParamsSchema.extend({
    /**
     * An opaque token representing the current pagination position.
     * If provided, the server should return results starting after this cursor.
     */
    cursor: external_exports.optional(CursorSchema)
  }).optional()
});
var PaginatedResultSchema = ResultSchema.extend({
  /**
   * An opaque token representing the pagination position after the last returned result.
   * If present, there may be more results available.
   */
  nextCursor: external_exports.optional(CursorSchema)
});
var ResourceContentsSchema = external_exports.object({
  /**
   * The URI of this resource.
   */
  uri: external_exports.string(),
  /**
   * The MIME type of this resource, if known.
   */
  mimeType: external_exports.optional(external_exports.string())
}).passthrough();
var TextResourceContentsSchema = ResourceContentsSchema.extend({
  /**
   * The text of the item. This must only be set if the item can actually be represented as text (not binary data).
   */
  text: external_exports.string()
});
var BlobResourceContentsSchema = ResourceContentsSchema.extend({
  /**
   * A base64-encoded string representing the binary data of the item.
   */
  blob: external_exports.string().base64()
});
var ResourceSchema = external_exports.object({
  /**
   * The URI of this resource.
   */
  uri: external_exports.string(),
  /**
   * A human-readable name for this resource.
   *
   * This can be used by clients to populate UI elements.
   */
  name: external_exports.string(),
  /**
   * A description of what this resource represents.
   *
   * This can be used by clients to improve the LLM's understanding of available resources. It can be thought of like a "hint" to the model.
   */
  description: external_exports.optional(external_exports.string()),
  /**
   * The MIME type of this resource, if known.
   */
  mimeType: external_exports.optional(external_exports.string())
}).passthrough();
var ResourceTemplateSchema = external_exports.object({
  /**
   * A URI template (according to RFC 6570) that can be used to construct resource URIs.
   */
  uriTemplate: external_exports.string(),
  /**
   * A human-readable name for the type of resource this template refers to.
   *
   * This can be used by clients to populate UI elements.
   */
  name: external_exports.string(),
  /**
   * A description of what this template is for.
   *
   * This can be used by clients to improve the LLM's understanding of available resources. It can be thought of like a "hint" to the model.
   */
  description: external_exports.optional(external_exports.string()),
  /**
   * The MIME type for all resources that match this template. This should only be included if all resources matching this template have the same type.
   */
  mimeType: external_exports.optional(external_exports.string())
}).passthrough();
var ListResourcesRequestSchema = PaginatedRequestSchema.extend({
  method: external_exports.literal("resources/list")
});
var ListResourcesResultSchema = PaginatedResultSchema.extend({
  resources: external_exports.array(ResourceSchema)
});
var ListResourceTemplatesRequestSchema = PaginatedRequestSchema.extend({
  method: external_exports.literal("resources/templates/list")
});
var ListResourceTemplatesResultSchema = PaginatedResultSchema.extend({
  resourceTemplates: external_exports.array(ResourceTemplateSchema)
});
var ReadResourceRequestSchema = RequestSchema.extend({
  method: external_exports.literal("resources/read"),
  params: BaseRequestParamsSchema.extend({
    /**
     * The URI of the resource to read. The URI can use any protocol; it is up to the server how to interpret it.
     */
    uri: external_exports.string()
  })
});
var ReadResourceResultSchema = ResultSchema.extend({
  contents: external_exports.array(external_exports.union([TextResourceContentsSchema, BlobResourceContentsSchema]))
});
var ResourceListChangedNotificationSchema = NotificationSchema.extend({
  method: external_exports.literal("notifications/resources/list_changed")
});
var SubscribeRequestSchema = RequestSchema.extend({
  method: external_exports.literal("resources/subscribe"),
  params: BaseRequestParamsSchema.extend({
    /**
     * The URI of the resource to subscribe to. The URI can use any protocol; it is up to the server how to interpret it.
     */
    uri: external_exports.string()
  })
});
var UnsubscribeRequestSchema = RequestSchema.extend({
  method: external_exports.literal("resources/unsubscribe"),
  params: BaseRequestParamsSchema.extend({
    /**
     * The URI of the resource to unsubscribe from.
     */
    uri: external_exports.string()
  })
});
var ResourceUpdatedNotificationSchema = NotificationSchema.extend({
  method: external_exports.literal("notifications/resources/updated"),
  params: BaseNotificationParamsSchema.extend({
    /**
     * The URI of the resource that has been updated. This might be a sub-resource of the one that the client actually subscribed to.
     */
    uri: external_exports.string()
  })
});
var PromptArgumentSchema = external_exports.object({
  /**
   * The name of the argument.
   */
  name: external_exports.string(),
  /**
   * A human-readable description of the argument.
   */
  description: external_exports.optional(external_exports.string()),
  /**
   * Whether this argument must be provided.
   */
  required: external_exports.optional(external_exports.boolean())
}).passthrough();
var PromptSchema = external_exports.object({
  /**
   * The name of the prompt or prompt template.
   */
  name: external_exports.string(),
  /**
   * An optional description of what this prompt provides
   */
  description: external_exports.optional(external_exports.string()),
  /**
   * A list of arguments to use for templating the prompt.
   */
  arguments: external_exports.optional(external_exports.array(PromptArgumentSchema))
}).passthrough();
var ListPromptsRequestSchema = PaginatedRequestSchema.extend({
  method: external_exports.literal("prompts/list")
});
var ListPromptsResultSchema = PaginatedResultSchema.extend({
  prompts: external_exports.array(PromptSchema)
});
var GetPromptRequestSchema = RequestSchema.extend({
  method: external_exports.literal("prompts/get"),
  params: BaseRequestParamsSchema.extend({
    /**
     * The name of the prompt or prompt template.
     */
    name: external_exports.string(),
    /**
     * Arguments to use for templating the prompt.
     */
    arguments: external_exports.optional(external_exports.record(external_exports.string()))
  })
});
var TextContentSchema = external_exports.object({
  type: external_exports.literal("text"),
  /**
   * The text content of the message.
   */
  text: external_exports.string()
}).passthrough();
var ImageContentSchema = external_exports.object({
  type: external_exports.literal("image"),
  /**
   * The base64-encoded image data.
   */
  data: external_exports.string().base64(),
  /**
   * The MIME type of the image. Different providers may support different image types.
   */
  mimeType: external_exports.string()
}).passthrough();
var AudioContentSchema = external_exports.object({
  type: external_exports.literal("audio"),
  /**
   * The base64-encoded audio data.
   */
  data: external_exports.string().base64(),
  /**
   * The MIME type of the audio. Different providers may support different audio types.
   */
  mimeType: external_exports.string()
}).passthrough();
var EmbeddedResourceSchema = external_exports.object({
  type: external_exports.literal("resource"),
  resource: external_exports.union([TextResourceContentsSchema, BlobResourceContentsSchema])
}).passthrough();
var PromptMessageSchema = external_exports.object({
  role: external_exports.enum(["user", "assistant"]),
  content: external_exports.union([
    TextContentSchema,
    ImageContentSchema,
    AudioContentSchema,
    EmbeddedResourceSchema
  ])
}).passthrough();
var GetPromptResultSchema = ResultSchema.extend({
  /**
   * An optional description for the prompt.
   */
  description: external_exports.optional(external_exports.string()),
  messages: external_exports.array(PromptMessageSchema)
});
var PromptListChangedNotificationSchema = NotificationSchema.extend({
  method: external_exports.literal("notifications/prompts/list_changed")
});
var ToolAnnotationsSchema = external_exports.object({
  /**
   * A human-readable title for the tool.
   */
  title: external_exports.optional(external_exports.string()),
  /**
   * If true, the tool does not modify its environment.
   *
   * Default: false
   */
  readOnlyHint: external_exports.optional(external_exports.boolean()),
  /**
   * If true, the tool may perform destructive updates to its environment.
   * If false, the tool performs only additive updates.
   *
   * (This property is meaningful only when `readOnlyHint == false`)
   *
   * Default: true
   */
  destructiveHint: external_exports.optional(external_exports.boolean()),
  /**
   * If true, calling the tool repeatedly with the same arguments
   * will have no additional effect on the its environment.
   *
   * (This property is meaningful only when `readOnlyHint == false`)
   *
   * Default: false
   */
  idempotentHint: external_exports.optional(external_exports.boolean()),
  /**
   * If true, this tool may interact with an "open world" of external
   * entities. If false, the tool's domain of interaction is closed.
   * For example, the world of a web search tool is open, whereas that
   * of a memory tool is not.
   *
   * Default: true
   */
  openWorldHint: external_exports.optional(external_exports.boolean())
}).passthrough();
var ToolSchema = external_exports.object({
  /**
   * The name of the tool.
   */
  name: external_exports.string(),
  /**
   * A human-readable description of the tool.
   */
  description: external_exports.optional(external_exports.string()),
  /**
   * A JSON Schema object defining the expected parameters for the tool.
   */
  inputSchema: external_exports.object({
    type: external_exports.literal("object"),
    properties: external_exports.optional(external_exports.object({}).passthrough()),
    required: external_exports.optional(external_exports.array(external_exports.string()))
  }).passthrough(),
  /**
   * An optional JSON Schema object defining the structure of the tool's output returned in
   * the structuredContent field of a CallToolResult.
   */
  outputSchema: external_exports.optional(external_exports.object({
    type: external_exports.literal("object"),
    properties: external_exports.optional(external_exports.object({}).passthrough()),
    required: external_exports.optional(external_exports.array(external_exports.string()))
  }).passthrough()),
  /**
   * Optional additional tool information.
   */
  annotations: external_exports.optional(ToolAnnotationsSchema)
}).passthrough();
var ListToolsRequestSchema = PaginatedRequestSchema.extend({
  method: external_exports.literal("tools/list")
});
var ListToolsResultSchema = PaginatedResultSchema.extend({
  tools: external_exports.array(ToolSchema)
});
var CallToolResultSchema = ResultSchema.extend({
  /**
   * A list of content objects that represent the result of the tool call.
   *
   * If the Tool does not define an outputSchema, this field MUST be present in the result.
   * For backwards compatibility, this field is always present, but it may be empty.
   */
  content: external_exports.array(external_exports.union([
    TextContentSchema,
    ImageContentSchema,
    AudioContentSchema,
    EmbeddedResourceSchema
  ])).default([]),
  /**
   * An object containing structured tool output.
   *
   * If the Tool defines an outputSchema, this field MUST be present in the result, and contain a JSON object that matches the schema.
   */
  structuredContent: external_exports.object({}).passthrough().optional(),
  /**
   * Whether the tool call ended in an error.
   *
   * If not set, this is assumed to be false (the call was successful).
   *
   * Any errors that originate from the tool SHOULD be reported inside the result
   * object, with `isError` set to true, _not_ as an MCP protocol-level error
   * response. Otherwise, the LLM would not be able to see that an error occurred
   * and self-correct.
   *
   * However, any errors in _finding_ the tool, an error indicating that the
   * server does not support tool calls, or any other exceptional conditions,
   * should be reported as an MCP error response.
   */
  isError: external_exports.optional(external_exports.boolean())
});
var CompatibilityCallToolResultSchema = CallToolResultSchema.or(ResultSchema.extend({
  toolResult: external_exports.unknown()
}));
var CallToolRequestSchema = RequestSchema.extend({
  method: external_exports.literal("tools/call"),
  params: BaseRequestParamsSchema.extend({
    name: external_exports.string(),
    arguments: external_exports.optional(external_exports.record(external_exports.unknown()))
  })
});
var ToolListChangedNotificationSchema = NotificationSchema.extend({
  method: external_exports.literal("notifications/tools/list_changed")
});
var LoggingLevelSchema = external_exports.enum([
  "debug",
  "info",
  "notice",
  "warning",
  "error",
  "critical",
  "alert",
  "emergency"
]);
var SetLevelRequestSchema = RequestSchema.extend({
  method: external_exports.literal("logging/setLevel"),
  params: BaseRequestParamsSchema.extend({
    /**
     * The level of logging that the client wants to receive from the server. The server should send all logs at this level and higher (i.e., more severe) to the client as notifications/logging/message.
     */
    level: LoggingLevelSchema
  })
});
var LoggingMessageNotificationSchema = NotificationSchema.extend({
  method: external_exports.literal("notifications/message"),
  params: BaseNotificationParamsSchema.extend({
    /**
     * The severity of this log message.
     */
    level: LoggingLevelSchema,
    /**
     * An optional name of the logger issuing this message.
     */
    logger: external_exports.optional(external_exports.string()),
    /**
     * The data to be logged, such as a string message or an object. Any JSON serializable type is allowed here.
     */
    data: external_exports.unknown()
  })
});
var ModelHintSchema = external_exports.object({
  /**
   * A hint for a model name.
   */
  name: external_exports.string().optional()
}).passthrough();
var ModelPreferencesSchema = external_exports.object({
  /**
   * Optional hints to use for model selection.
   */
  hints: external_exports.optional(external_exports.array(ModelHintSchema)),
  /**
   * How much to prioritize cost when selecting a model.
   */
  costPriority: external_exports.optional(external_exports.number().min(0).max(1)),
  /**
   * How much to prioritize sampling speed (latency) when selecting a model.
   */
  speedPriority: external_exports.optional(external_exports.number().min(0).max(1)),
  /**
   * How much to prioritize intelligence and capabilities when selecting a model.
   */
  intelligencePriority: external_exports.optional(external_exports.number().min(0).max(1))
}).passthrough();
var SamplingMessageSchema = external_exports.object({
  role: external_exports.enum(["user", "assistant"]),
  content: external_exports.union([TextContentSchema, ImageContentSchema, AudioContentSchema])
}).passthrough();
var CreateMessageRequestSchema = RequestSchema.extend({
  method: external_exports.literal("sampling/createMessage"),
  params: BaseRequestParamsSchema.extend({
    messages: external_exports.array(SamplingMessageSchema),
    /**
     * An optional system prompt the server wants to use for sampling. The client MAY modify or omit this prompt.
     */
    systemPrompt: external_exports.optional(external_exports.string()),
    /**
     * A request to include context from one or more MCP servers (including the caller), to be attached to the prompt. The client MAY ignore this request.
     */
    includeContext: external_exports.optional(external_exports.enum(["none", "thisServer", "allServers"])),
    temperature: external_exports.optional(external_exports.number()),
    /**
     * The maximum number of tokens to sample, as requested by the server. The client MAY choose to sample fewer tokens than requested.
     */
    maxTokens: external_exports.number().int(),
    stopSequences: external_exports.optional(external_exports.array(external_exports.string())),
    /**
     * Optional metadata to pass through to the LLM provider. The format of this metadata is provider-specific.
     */
    metadata: external_exports.optional(external_exports.object({}).passthrough()),
    /**
     * The server's preferences for which model to select.
     */
    modelPreferences: external_exports.optional(ModelPreferencesSchema)
  })
});
var CreateMessageResultSchema = ResultSchema.extend({
  /**
   * The name of the model that generated the message.
   */
  model: external_exports.string(),
  /**
   * The reason why sampling stopped.
   */
  stopReason: external_exports.optional(external_exports.enum(["endTurn", "stopSequence", "maxTokens"]).or(external_exports.string())),
  role: external_exports.enum(["user", "assistant"]),
  content: external_exports.discriminatedUnion("type", [
    TextContentSchema,
    ImageContentSchema,
    AudioContentSchema
  ])
});
var ResourceReferenceSchema = external_exports.object({
  type: external_exports.literal("ref/resource"),
  /**
   * The URI or URI template of the resource.
   */
  uri: external_exports.string()
}).passthrough();
var PromptReferenceSchema = external_exports.object({
  type: external_exports.literal("ref/prompt"),
  /**
   * The name of the prompt or prompt template
   */
  name: external_exports.string()
}).passthrough();
var CompleteRequestSchema = RequestSchema.extend({
  method: external_exports.literal("completion/complete"),
  params: BaseRequestParamsSchema.extend({
    ref: external_exports.union([PromptReferenceSchema, ResourceReferenceSchema]),
    /**
     * The argument's information
     */
    argument: external_exports.object({
      /**
       * The name of the argument
       */
      name: external_exports.string(),
      /**
       * The value of the argument to use for completion matching.
       */
      value: external_exports.string()
    }).passthrough()
  })
});
var CompleteResultSchema = ResultSchema.extend({
  completion: external_exports.object({
    /**
     * An array of completion values. Must not exceed 100 items.
     */
    values: external_exports.array(external_exports.string()).max(100),
    /**
     * The total number of completion options available. This can exceed the number of values actually sent in the response.
     */
    total: external_exports.optional(external_exports.number().int()),
    /**
     * Indicates whether there are additional completion options beyond those provided in the current response, even if the exact total is unknown.
     */
    hasMore: external_exports.optional(external_exports.boolean())
  }).passthrough()
});
var RootSchema = external_exports.object({
  /**
   * The URI identifying the root. This *must* start with file:// for now.
   */
  uri: external_exports.string().startsWith("file://"),
  /**
   * An optional name for the root.
   */
  name: external_exports.optional(external_exports.string())
}).passthrough();
var ListRootsRequestSchema = RequestSchema.extend({
  method: external_exports.literal("roots/list")
});
var ListRootsResultSchema = ResultSchema.extend({
  roots: external_exports.array(RootSchema)
});
var RootsListChangedNotificationSchema = NotificationSchema.extend({
  method: external_exports.literal("notifications/roots/list_changed")
});
var ClientRequestSchema = external_exports.union([
  PingRequestSchema,
  InitializeRequestSchema,
  CompleteRequestSchema,
  SetLevelRequestSchema,
  GetPromptRequestSchema,
  ListPromptsRequestSchema,
  ListResourcesRequestSchema,
  ListResourceTemplatesRequestSchema,
  ReadResourceRequestSchema,
  SubscribeRequestSchema,
  UnsubscribeRequestSchema,
  CallToolRequestSchema,
  ListToolsRequestSchema
]);
var ClientNotificationSchema = external_exports.union([
  CancelledNotificationSchema,
  ProgressNotificationSchema,
  InitializedNotificationSchema,
  RootsListChangedNotificationSchema
]);
var ClientResultSchema = external_exports.union([
  EmptyResultSchema,
  CreateMessageResultSchema,
  ListRootsResultSchema
]);
var ServerRequestSchema = external_exports.union([
  PingRequestSchema,
  CreateMessageRequestSchema,
  ListRootsRequestSchema
]);
var ServerNotificationSchema = external_exports.union([
  CancelledNotificationSchema,
  ProgressNotificationSchema,
  LoggingMessageNotificationSchema,
  ResourceUpdatedNotificationSchema,
  ResourceListChangedNotificationSchema,
  ToolListChangedNotificationSchema,
  PromptListChangedNotificationSchema
]);
var ServerResultSchema = external_exports.union([
  EmptyResultSchema,
  InitializeResultSchema,
  CompleteResultSchema,
  GetPromptResultSchema,
  ListPromptsResultSchema,
  ListResourcesResultSchema,
  ListResourceTemplatesResultSchema,
  ReadResourceResultSchema,
  CallToolResultSchema,
  ListToolsResultSchema
]);
var McpError = class extends Error {
  constructor(code, message, data) {
    super(`MCP error ${code}: ${message}`);
    this.code = code;
    this.data = data;
    this.name = "McpError";
  }
};

// node_modules/@modelcontextprotocol/sdk/dist/esm/shared/protocol.js
var DEFAULT_REQUEST_TIMEOUT_MSEC = 6e4;
var Protocol = class {
  constructor(_options) {
    this._options = _options;
    this._requestMessageId = 0;
    this._requestHandlers = /* @__PURE__ */ new Map();
    this._requestHandlerAbortControllers = /* @__PURE__ */ new Map();
    this._notificationHandlers = /* @__PURE__ */ new Map();
    this._responseHandlers = /* @__PURE__ */ new Map();
    this._progressHandlers = /* @__PURE__ */ new Map();
    this._timeoutInfo = /* @__PURE__ */ new Map();
    this.setNotificationHandler(CancelledNotificationSchema, (notification) => {
      const controller = this._requestHandlerAbortControllers.get(notification.params.requestId);
      controller === null || controller === void 0 ? void 0 : controller.abort(notification.params.reason);
    });
    this.setNotificationHandler(ProgressNotificationSchema, (notification) => {
      this._onprogress(notification);
    });
    this.setRequestHandler(
      PingRequestSchema,
      // Automatic pong by default.
      (_request) => ({})
    );
  }
  _setupTimeout(messageId, timeout, maxTotalTimeout, onTimeout, resetTimeoutOnProgress = false) {
    this._timeoutInfo.set(messageId, {
      timeoutId: setTimeout(onTimeout, timeout),
      startTime: Date.now(),
      timeout,
      maxTotalTimeout,
      resetTimeoutOnProgress,
      onTimeout
    });
  }
  _resetTimeout(messageId) {
    const info = this._timeoutInfo.get(messageId);
    if (!info)
      return false;
    const totalElapsed = Date.now() - info.startTime;
    if (info.maxTotalTimeout && totalElapsed >= info.maxTotalTimeout) {
      this._timeoutInfo.delete(messageId);
      throw new McpError(ErrorCode.RequestTimeout, "Maximum total timeout exceeded", { maxTotalTimeout: info.maxTotalTimeout, totalElapsed });
    }
    clearTimeout(info.timeoutId);
    info.timeoutId = setTimeout(info.onTimeout, info.timeout);
    return true;
  }
  _cleanupTimeout(messageId) {
    const info = this._timeoutInfo.get(messageId);
    if (info) {
      clearTimeout(info.timeoutId);
      this._timeoutInfo.delete(messageId);
    }
  }
  /**
   * Attaches to the given transport, starts it, and starts listening for messages.
   *
   * The Protocol object assumes ownership of the Transport, replacing any callbacks that have already been set, and expects that it is the only user of the Transport instance going forward.
   */
  async connect(transport) {
    this._transport = transport;
    this._transport.onclose = () => {
      this._onclose();
    };
    this._transport.onerror = (error) => {
      this._onerror(error);
    };
    this._transport.onmessage = (message, extra) => {
      if (isJSONRPCResponse(message) || isJSONRPCError(message)) {
        this._onresponse(message);
      } else if (isJSONRPCRequest(message)) {
        this._onrequest(message, extra);
      } else if (isJSONRPCNotification(message)) {
        this._onnotification(message);
      } else {
        this._onerror(new Error(`Unknown message type: ${JSON.stringify(message)}`));
      }
    };
    await this._transport.start();
  }
  _onclose() {
    var _a;
    const responseHandlers = this._responseHandlers;
    this._responseHandlers = /* @__PURE__ */ new Map();
    this._progressHandlers.clear();
    this._transport = void 0;
    (_a = this.onclose) === null || _a === void 0 ? void 0 : _a.call(this);
    const error = new McpError(ErrorCode.ConnectionClosed, "Connection closed");
    for (const handler of responseHandlers.values()) {
      handler(error);
    }
  }
  _onerror(error) {
    var _a;
    (_a = this.onerror) === null || _a === void 0 ? void 0 : _a.call(this, error);
  }
  _onnotification(notification) {
    var _a;
    const handler = (_a = this._notificationHandlers.get(notification.method)) !== null && _a !== void 0 ? _a : this.fallbackNotificationHandler;
    if (handler === void 0) {
      return;
    }
    Promise.resolve().then(() => handler(notification)).catch((error) => this._onerror(new Error(`Uncaught error in notification handler: ${error}`)));
  }
  _onrequest(request, extra) {
    var _a, _b, _c, _d;
    const handler = (_a = this._requestHandlers.get(request.method)) !== null && _a !== void 0 ? _a : this.fallbackRequestHandler;
    if (handler === void 0) {
      (_b = this._transport) === null || _b === void 0 ? void 0 : _b.send({
        jsonrpc: "2.0",
        id: request.id,
        error: {
          code: ErrorCode.MethodNotFound,
          message: "Method not found"
        }
      }).catch((error) => this._onerror(new Error(`Failed to send an error response: ${error}`)));
      return;
    }
    const abortController = new AbortController();
    this._requestHandlerAbortControllers.set(request.id, abortController);
    const fullExtra = {
      signal: abortController.signal,
      sessionId: (_c = this._transport) === null || _c === void 0 ? void 0 : _c.sessionId,
      _meta: (_d = request.params) === null || _d === void 0 ? void 0 : _d._meta,
      sendNotification: (notification) => this.notification(notification, { relatedRequestId: request.id }),
      sendRequest: (r, resultSchema, options) => this.request(r, resultSchema, { ...options, relatedRequestId: request.id }),
      authInfo: extra === null || extra === void 0 ? void 0 : extra.authInfo,
      requestId: request.id
    };
    Promise.resolve().then(() => handler(request, fullExtra)).then((result) => {
      var _a2;
      if (abortController.signal.aborted) {
        return;
      }
      return (_a2 = this._transport) === null || _a2 === void 0 ? void 0 : _a2.send({
        result,
        jsonrpc: "2.0",
        id: request.id
      });
    }, (error) => {
      var _a2, _b2;
      if (abortController.signal.aborted) {
        return;
      }
      return (_a2 = this._transport) === null || _a2 === void 0 ? void 0 : _a2.send({
        jsonrpc: "2.0",
        id: request.id,
        error: {
          code: Number.isSafeInteger(error["code"]) ? error["code"] : ErrorCode.InternalError,
          message: (_b2 = error.message) !== null && _b2 !== void 0 ? _b2 : "Internal error"
        }
      });
    }).catch((error) => this._onerror(new Error(`Failed to send response: ${error}`))).finally(() => {
      this._requestHandlerAbortControllers.delete(request.id);
    });
  }
  _onprogress(notification) {
    const { progressToken, ...params } = notification.params;
    const messageId = Number(progressToken);
    const handler = this._progressHandlers.get(messageId);
    if (!handler) {
      this._onerror(new Error(`Received a progress notification for an unknown token: ${JSON.stringify(notification)}`));
      return;
    }
    const responseHandler = this._responseHandlers.get(messageId);
    const timeoutInfo = this._timeoutInfo.get(messageId);
    if (timeoutInfo && responseHandler && timeoutInfo.resetTimeoutOnProgress) {
      try {
        this._resetTimeout(messageId);
      } catch (error) {
        responseHandler(error);
        return;
      }
    }
    handler(params);
  }
  _onresponse(response) {
    const messageId = Number(response.id);
    const handler = this._responseHandlers.get(messageId);
    if (handler === void 0) {
      this._onerror(new Error(`Received a response for an unknown message ID: ${JSON.stringify(response)}`));
      return;
    }
    this._responseHandlers.delete(messageId);
    this._progressHandlers.delete(messageId);
    this._cleanupTimeout(messageId);
    if (isJSONRPCResponse(response)) {
      handler(response);
    } else {
      const error = new McpError(response.error.code, response.error.message, response.error.data);
      handler(error);
    }
  }
  get transport() {
    return this._transport;
  }
  /**
   * Closes the connection.
   */
  async close() {
    var _a;
    await ((_a = this._transport) === null || _a === void 0 ? void 0 : _a.close());
  }
  /**
   * Sends a request and wait for a response.
   *
   * Do not use this method to emit notifications! Use notification() instead.
   */
  request(request, resultSchema, options) {
    const { relatedRequestId, resumptionToken, onresumptiontoken } = options !== null && options !== void 0 ? options : {};
    return new Promise((resolve2, reject) => {
      var _a, _b, _c, _d, _e, _f;
      if (!this._transport) {
        reject(new Error("Not connected"));
        return;
      }
      if (((_a = this._options) === null || _a === void 0 ? void 0 : _a.enforceStrictCapabilities) === true) {
        this.assertCapabilityForMethod(request.method);
      }
      (_b = options === null || options === void 0 ? void 0 : options.signal) === null || _b === void 0 ? void 0 : _b.throwIfAborted();
      const messageId = this._requestMessageId++;
      const jsonrpcRequest = {
        ...request,
        jsonrpc: "2.0",
        id: messageId
      };
      if (options === null || options === void 0 ? void 0 : options.onprogress) {
        this._progressHandlers.set(messageId, options.onprogress);
        jsonrpcRequest.params = {
          ...request.params,
          _meta: {
            ...((_c = request.params) === null || _c === void 0 ? void 0 : _c._meta) || {},
            progressToken: messageId
          }
        };
      }
      const cancel = (reason) => {
        var _a2;
        this._responseHandlers.delete(messageId);
        this._progressHandlers.delete(messageId);
        this._cleanupTimeout(messageId);
        (_a2 = this._transport) === null || _a2 === void 0 ? void 0 : _a2.send({
          jsonrpc: "2.0",
          method: "notifications/cancelled",
          params: {
            requestId: messageId,
            reason: String(reason)
          }
        }, { relatedRequestId, resumptionToken, onresumptiontoken }).catch((error) => this._onerror(new Error(`Failed to send cancellation: ${error}`)));
        reject(reason);
      };
      this._responseHandlers.set(messageId, (response) => {
        var _a2;
        if ((_a2 = options === null || options === void 0 ? void 0 : options.signal) === null || _a2 === void 0 ? void 0 : _a2.aborted) {
          return;
        }
        if (response instanceof Error) {
          return reject(response);
        }
        try {
          const result = resultSchema.parse(response.result);
          resolve2(result);
        } catch (error) {
          reject(error);
        }
      });
      (_d = options === null || options === void 0 ? void 0 : options.signal) === null || _d === void 0 ? void 0 : _d.addEventListener("abort", () => {
        var _a2;
        cancel((_a2 = options === null || options === void 0 ? void 0 : options.signal) === null || _a2 === void 0 ? void 0 : _a2.reason);
      });
      const timeout = (_e = options === null || options === void 0 ? void 0 : options.timeout) !== null && _e !== void 0 ? _e : DEFAULT_REQUEST_TIMEOUT_MSEC;
      const timeoutHandler = () => cancel(new McpError(ErrorCode.RequestTimeout, "Request timed out", { timeout }));
      this._setupTimeout(messageId, timeout, options === null || options === void 0 ? void 0 : options.maxTotalTimeout, timeoutHandler, (_f = options === null || options === void 0 ? void 0 : options.resetTimeoutOnProgress) !== null && _f !== void 0 ? _f : false);
      this._transport.send(jsonrpcRequest, { relatedRequestId, resumptionToken, onresumptiontoken }).catch((error) => {
        this._cleanupTimeout(messageId);
        reject(error);
      });
    });
  }
  /**
   * Emits a notification, which is a one-way message that does not expect a response.
   */
  async notification(notification, options) {
    if (!this._transport) {
      throw new Error("Not connected");
    }
    this.assertNotificationCapability(notification.method);
    const jsonrpcNotification = {
      ...notification,
      jsonrpc: "2.0"
    };
    await this._transport.send(jsonrpcNotification, options);
  }
  /**
   * Registers a handler to invoke when this protocol object receives a request with the given method.
   *
   * Note that this will replace any previous request handler for the same method.
   */
  setRequestHandler(requestSchema, handler) {
    const method = requestSchema.shape.method.value;
    this.assertRequestHandlerCapability(method);
    this._requestHandlers.set(method, (request, extra) => {
      return Promise.resolve(handler(requestSchema.parse(request), extra));
    });
  }
  /**
   * Removes the request handler for the given method.
   */
  removeRequestHandler(method) {
    this._requestHandlers.delete(method);
  }
  /**
   * Asserts that a request handler has not already been set for the given method, in preparation for a new one being automatically installed.
   */
  assertCanSetRequestHandler(method) {
    if (this._requestHandlers.has(method)) {
      throw new Error(`A request handler for ${method} already exists, which would be overridden`);
    }
  }
  /**
   * Registers a handler to invoke when this protocol object receives a notification with the given method.
   *
   * Note that this will replace any previous notification handler for the same method.
   */
  setNotificationHandler(notificationSchema, handler) {
    this._notificationHandlers.set(notificationSchema.shape.method.value, (notification) => Promise.resolve(handler(notificationSchema.parse(notification))));
  }
  /**
   * Removes the notification handler for the given method.
   */
  removeNotificationHandler(method) {
    this._notificationHandlers.delete(method);
  }
};
function mergeCapabilities(base, additional) {
  return Object.entries(additional).reduce((acc, [key, value]) => {
    if (value && typeof value === "object") {
      acc[key] = acc[key] ? { ...acc[key], ...value } : value;
    } else {
      acc[key] = value;
    }
    return acc;
  }, { ...base });
}

// node_modules/@modelcontextprotocol/sdk/dist/esm/server/index.js
var Server = class extends Protocol {
  /**
   * Initializes this server with the given name and version information.
   */
  constructor(_serverInfo, options) {
    var _a;
    super(options);
    this._serverInfo = _serverInfo;
    this._capabilities = (_a = options === null || options === void 0 ? void 0 : options.capabilities) !== null && _a !== void 0 ? _a : {};
    this._instructions = options === null || options === void 0 ? void 0 : options.instructions;
    this.setRequestHandler(InitializeRequestSchema, (request) => this._oninitialize(request));
    this.setNotificationHandler(InitializedNotificationSchema, () => {
      var _a2;
      return (_a2 = this.oninitialized) === null || _a2 === void 0 ? void 0 : _a2.call(this);
    });
  }
  /**
   * Registers new capabilities. This can only be called before connecting to a transport.
   *
   * The new capabilities will be merged with any existing capabilities previously given (e.g., at initialization).
   */
  registerCapabilities(capabilities) {
    if (this.transport) {
      throw new Error("Cannot register capabilities after connecting to transport");
    }
    this._capabilities = mergeCapabilities(this._capabilities, capabilities);
  }
  assertCapabilityForMethod(method) {
    var _a, _b;
    switch (method) {
      case "sampling/createMessage":
        if (!((_a = this._clientCapabilities) === null || _a === void 0 ? void 0 : _a.sampling)) {
          throw new Error(`Client does not support sampling (required for ${method})`);
        }
        break;
      case "roots/list":
        if (!((_b = this._clientCapabilities) === null || _b === void 0 ? void 0 : _b.roots)) {
          throw new Error(`Client does not support listing roots (required for ${method})`);
        }
        break;
      case "ping":
        break;
    }
  }
  assertNotificationCapability(method) {
    switch (method) {
      case "notifications/message":
        if (!this._capabilities.logging) {
          throw new Error(`Server does not support logging (required for ${method})`);
        }
        break;
      case "notifications/resources/updated":
      case "notifications/resources/list_changed":
        if (!this._capabilities.resources) {
          throw new Error(`Server does not support notifying about resources (required for ${method})`);
        }
        break;
      case "notifications/tools/list_changed":
        if (!this._capabilities.tools) {
          throw new Error(`Server does not support notifying of tool list changes (required for ${method})`);
        }
        break;
      case "notifications/prompts/list_changed":
        if (!this._capabilities.prompts) {
          throw new Error(`Server does not support notifying of prompt list changes (required for ${method})`);
        }
        break;
      case "notifications/cancelled":
        break;
      case "notifications/progress":
        break;
    }
  }
  assertRequestHandlerCapability(method) {
    switch (method) {
      case "sampling/createMessage":
        if (!this._capabilities.sampling) {
          throw new Error(`Server does not support sampling (required for ${method})`);
        }
        break;
      case "logging/setLevel":
        if (!this._capabilities.logging) {
          throw new Error(`Server does not support logging (required for ${method})`);
        }
        break;
      case "prompts/get":
      case "prompts/list":
        if (!this._capabilities.prompts) {
          throw new Error(`Server does not support prompts (required for ${method})`);
        }
        break;
      case "resources/list":
      case "resources/templates/list":
      case "resources/read":
        if (!this._capabilities.resources) {
          throw new Error(`Server does not support resources (required for ${method})`);
        }
        break;
      case "tools/call":
      case "tools/list":
        if (!this._capabilities.tools) {
          throw new Error(`Server does not support tools (required for ${method})`);
        }
        break;
      case "ping":
      case "initialize":
        break;
    }
  }
  async _oninitialize(request) {
    const requestedVersion = request.params.protocolVersion;
    this._clientCapabilities = request.params.capabilities;
    this._clientVersion = request.params.clientInfo;
    return {
      protocolVersion: SUPPORTED_PROTOCOL_VERSIONS.includes(requestedVersion) ? requestedVersion : LATEST_PROTOCOL_VERSION,
      capabilities: this.getCapabilities(),
      serverInfo: this._serverInfo,
      ...this._instructions && { instructions: this._instructions }
    };
  }
  /**
   * After initialization has completed, this will be populated with the client's reported capabilities.
   */
  getClientCapabilities() {
    return this._clientCapabilities;
  }
  /**
   * After initialization has completed, this will be populated with information about the client's name and version.
   */
  getClientVersion() {
    return this._clientVersion;
  }
  getCapabilities() {
    return this._capabilities;
  }
  async ping() {
    return this.request({ method: "ping" }, EmptyResultSchema);
  }
  async createMessage(params, options) {
    return this.request({ method: "sampling/createMessage", params }, CreateMessageResultSchema, options);
  }
  async listRoots(params, options) {
    return this.request({ method: "roots/list", params }, ListRootsResultSchema, options);
  }
  async sendLoggingMessage(params) {
    return this.notification({ method: "notifications/message", params });
  }
  async sendResourceUpdated(params) {
    return this.notification({
      method: "notifications/resources/updated",
      params
    });
  }
  async sendResourceListChanged() {
    return this.notification({
      method: "notifications/resources/list_changed"
    });
  }
  async sendToolListChanged() {
    return this.notification({ method: "notifications/tools/list_changed" });
  }
  async sendPromptListChanged() {
    return this.notification({ method: "notifications/prompts/list_changed" });
  }
};

// node_modules/@modelcontextprotocol/sdk/dist/esm/server/stdio.js
import process2 from "node:process";

// node_modules/@modelcontextprotocol/sdk/dist/esm/shared/stdio.js
var ReadBuffer = class {
  append(chunk) {
    this._buffer = this._buffer ? Buffer.concat([this._buffer, chunk]) : chunk;
  }
  readMessage() {
    if (!this._buffer) {
      return null;
    }
    const index = this._buffer.indexOf("\n");
    if (index === -1) {
      return null;
    }
    const line = this._buffer.toString("utf8", 0, index).replace(/\r$/, "");
    this._buffer = this._buffer.subarray(index + 1);
    return deserializeMessage(line);
  }
  clear() {
    this._buffer = void 0;
  }
};
function deserializeMessage(line) {
  return JSONRPCMessageSchema.parse(JSON.parse(line));
}
function serializeMessage(message) {
  return JSON.stringify(message) + "\n";
}

// node_modules/@modelcontextprotocol/sdk/dist/esm/server/stdio.js
var StdioServerTransport = class {
  constructor(_stdin = process2.stdin, _stdout = process2.stdout) {
    this._stdin = _stdin;
    this._stdout = _stdout;
    this._readBuffer = new ReadBuffer();
    this._started = false;
    this._ondata = (chunk) => {
      this._readBuffer.append(chunk);
      this.processReadBuffer();
    };
    this._onerror = (error) => {
      var _a;
      (_a = this.onerror) === null || _a === void 0 ? void 0 : _a.call(this, error);
    };
  }
  /**
   * Starts listening for messages on stdin.
   */
  async start() {
    if (this._started) {
      throw new Error("StdioServerTransport already started! If using Server class, note that connect() calls start() automatically.");
    }
    this._started = true;
    this._stdin.on("data", this._ondata);
    this._stdin.on("error", this._onerror);
  }
  processReadBuffer() {
    var _a, _b;
    while (true) {
      try {
        const message = this._readBuffer.readMessage();
        if (message === null) {
          break;
        }
        (_a = this.onmessage) === null || _a === void 0 ? void 0 : _a.call(this, message);
      } catch (error) {
        (_b = this.onerror) === null || _b === void 0 ? void 0 : _b.call(this, error);
      }
    }
  }
  async close() {
    var _a;
    this._stdin.off("data", this._ondata);
    this._stdin.off("error", this._onerror);
    const remainingDataListeners = this._stdin.listenerCount("data");
    if (remainingDataListeners === 0) {
      this._stdin.pause();
    }
    this._readBuffer.clear();
    (_a = this.onclose) === null || _a === void 0 ? void 0 : _a.call(this);
  }
  send(message) {
    return new Promise((resolve2) => {
      const json = serializeMessage(message);
      if (this._stdout.write(json)) {
        resolve2();
      } else {
        this._stdout.once("drain", resolve2);
      }
    });
  }
};

// src/unity-client.ts
import * as net from "net";

// src/constants.ts
var MCP_PROTOCOL_VERSION = "2024-11-05";
var MCP_SERVER_NAME = "uloopmcp-server";
var TOOLS_LIST_CHANGED_CAPABILITY = true;
var NOTIFICATION_METHODS = {
  TOOLS_LIST_CHANGED: "notifications/tools/list_changed"
};
var UNITY_CONNECTION = {
  DEFAULT_PORT: "8700",
  DEFAULT_HOST: "127.0.0.1",
  CONNECTION_TEST_MESSAGE: "connection_test"
};
var JSONRPC = {
  VERSION: "2.0"
};
var PARAMETER_SCHEMA = {
  TYPE_PROPERTY: "Type",
  DESCRIPTION_PROPERTY: "Description",
  DEFAULT_VALUE_PROPERTY: "DefaultValue",
  ENUM_PROPERTY: "Enum",
  PROPERTIES_PROPERTY: "Properties",
  REQUIRED_PROPERTY: "Required"
};
var TIMEOUTS = {
  NETWORK: 12e4
  // 2分 - ネットワークレベルのタイムアウト（Unity側のタイムアウトより長く設定）
};
var DEFAULT_CLIENT_NAME = "";
var ENVIRONMENT = {
  NODE_ENV_DEVELOPMENT: "development",
  NODE_ENV_PRODUCTION: "production"
};
var ERROR_MESSAGES = {
  NOT_CONNECTED: "Unity MCP Bridge is not connected",
  CONNECTION_FAILED: "Unity connection failed",
  TIMEOUT: "timeout",
  INVALID_RESPONSE: "Invalid response from Unity"
};
var POLLING = {
  INTERVAL_MS: 1e3,
  // Reduced from 3000ms to 1000ms for better responsiveness
  BUFFER_SECONDS: 15,
  // Increased for safer Unity startup timing
  // Adaptive polling configuration
  INITIAL_ATTEMPTS: 1,
  // Number of initial attempts with fast polling
  INITIAL_INTERVAL_MS: 1e3,
  // Fast polling interval for initial attempts
  EXTENDED_INTERVAL_MS: 1e4
  // Slower polling interval after initial attempts
};
var LIST_CHANGED_SUPPORTED_CLIENTS = ["cursor", "mcp-inspector"];
var OUTPUT_DIRECTORIES = {
  ROOT: "uLoopMCPOutputs",
  VIBE_LOGS: "VibeLogs"
};

// src/utils/safe-timer.ts
var SafeTimer = class _SafeTimer {
  static activeTimers = /* @__PURE__ */ new Set();
  static cleanupHandlersInstalled = false;
  timerId = null;
  isActive = false;
  callback;
  delay;
  isInterval;
  constructor(callback, delay, isInterval = false) {
    this.callback = callback;
    this.delay = delay;
    this.isInterval = isInterval;
    _SafeTimer.installCleanupHandlers();
    this.start();
  }
  /**
   * Start the timer
   */
  start() {
    if (this.isActive) {
      return;
    }
    if (this.isInterval) {
      this.timerId = setInterval(this.callback, this.delay);
    } else {
      this.timerId = setTimeout(this.callback, this.delay);
    }
    this.isActive = true;
    _SafeTimer.activeTimers.add(this);
  }
  /**
   * Stop and clean up the timer
   */
  stop() {
    if (!this.isActive || !this.timerId) {
      return;
    }
    if (this.isInterval) {
      clearInterval(this.timerId);
    } else {
      clearTimeout(this.timerId);
    }
    this.timerId = null;
    this.isActive = false;
    _SafeTimer.activeTimers.delete(this);
  }
  /**
   * Check if timer is currently active
   */
  get active() {
    return this.isActive;
  }
  /**
   * Install global cleanup handlers to ensure all timers are cleaned up
   */
  static installCleanupHandlers() {
    if (_SafeTimer.cleanupHandlersInstalled) {
      return;
    }
    const cleanup = () => {
      _SafeTimer.cleanupAll();
    };
    process.on("exit", cleanup);
    process.on("SIGINT", cleanup);
    process.on("SIGTERM", cleanup);
    process.on("SIGHUP", cleanup);
    process.on("uncaughtException", cleanup);
    process.on("unhandledRejection", cleanup);
    _SafeTimer.cleanupHandlersInstalled = true;
  }
  /**
   * Clean up all active timers
   */
  static cleanupAll() {
    for (const timer of _SafeTimer.activeTimers) {
      timer.stop();
    }
    _SafeTimer.activeTimers.clear();
  }
  /**
   * Get count of active timers (for debugging)
   */
  static getActiveTimerCount() {
    return _SafeTimer.activeTimers.size;
  }
};
function safeSetTimeout(callback, delay) {
  return new SafeTimer(callback, delay, false);
}
function stopSafeTimer(timer) {
  if (!timer) {
    return;
  }
  timer.stop();
}

// src/utils/vibe-logger.ts
import * as fs from "fs";
import * as path from "path";
import { fileURLToPath } from "url";

// node_modules/uuid/dist/esm/stringify.js
var byteToHex = [];
for (let i = 0; i < 256; ++i) {
  byteToHex.push((i + 256).toString(16).slice(1));
}
function unsafeStringify(arr, offset = 0) {
  return (byteToHex[arr[offset + 0]] + byteToHex[arr[offset + 1]] + byteToHex[arr[offset + 2]] + byteToHex[arr[offset + 3]] + "-" + byteToHex[arr[offset + 4]] + byteToHex[arr[offset + 5]] + "-" + byteToHex[arr[offset + 6]] + byteToHex[arr[offset + 7]] + "-" + byteToHex[arr[offset + 8]] + byteToHex[arr[offset + 9]] + "-" + byteToHex[arr[offset + 10]] + byteToHex[arr[offset + 11]] + byteToHex[arr[offset + 12]] + byteToHex[arr[offset + 13]] + byteToHex[arr[offset + 14]] + byteToHex[arr[offset + 15]]).toLowerCase();
}

// node_modules/uuid/dist/esm/rng.js
import { randomFillSync } from "crypto";
var rnds8Pool = new Uint8Array(256);
var poolPtr = rnds8Pool.length;
function rng() {
  if (poolPtr > rnds8Pool.length - 16) {
    randomFillSync(rnds8Pool);
    poolPtr = 0;
  }
  return rnds8Pool.slice(poolPtr, poolPtr += 16);
}

// node_modules/uuid/dist/esm/native.js
import { randomUUID } from "crypto";
var native_default = { randomUUID };

// node_modules/uuid/dist/esm/v4.js
function v4(options, buf, offset) {
  if (native_default.randomUUID && !buf && !options) {
    return native_default.randomUUID();
  }
  options = options || {};
  const rnds = options.random ?? options.rng?.() ?? rng();
  if (rnds.length < 16) {
    throw new Error("Random bytes length must be >= 16");
  }
  rnds[6] = rnds[6] & 15 | 64;
  rnds[8] = rnds[8] & 63 | 128;
  if (buf) {
    offset = offset || 0;
    if (offset < 0 || offset + 16 > buf.length) {
      throw new RangeError(`UUID byte range ${offset}:${offset + 15} is out of buffer bounds`);
    }
    for (let i = 0; i < 16; ++i) {
      buf[offset + i] = rnds[i];
    }
    return buf;
  }
  return unsafeStringify(rnds);
}
var v4_default = v4;

// src/utils/vibe-logger.ts
var VibeLogger = class _VibeLogger {
  // Navigate from TypeScriptServer~ to project root: ../../../
  static PROJECT_ROOT = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../../../.."
  );
  static LOG_DIRECTORY = path.join(
    _VibeLogger.PROJECT_ROOT,
    OUTPUT_DIRECTORIES.ROOT,
    OUTPUT_DIRECTORIES.VIBE_LOGS
  );
  static LOG_FILE_PREFIX = "ts_vibe";
  static MAX_FILE_SIZE_MB = 10;
  static MAX_MEMORY_LOGS = 1e3;
  static memoryLogs = [];
  static isDebugEnabled = process.env.MCP_DEBUG === "true";
  /**
   * Log an info level message with structured context
   */
  static logInfo(operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace) {
    _VibeLogger.log(
      "INFO",
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Log a warning level message with structured context and optional stack trace
   */
  static logWarning(operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace = true) {
    _VibeLogger.log(
      "WARNING",
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Log an error level message with structured context and optional stack trace
   */
  static logError(operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace = true) {
    _VibeLogger.log(
      "ERROR",
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Log a debug level message with structured context and optional stack trace
   */
  static logDebug(operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace) {
    _VibeLogger.log(
      "DEBUG",
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Log an exception with structured context and optional additional stack trace
   */
  static logException(operation, error, context, correlationId, humanNote, aiTodo, includeStackTrace) {
    const exceptionContext = {
      original_context: context,
      exception: {
        name: error.name,
        message: error.message,
        stack: error.stack,
        cause: error.cause
      }
    };
    _VibeLogger.log(
      "ERROR",
      operation,
      `Exception occurred: ${error.message}`,
      exceptionContext,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Generate a new correlation ID for tracking related operations
   */
  static generateCorrelationId() {
    const timestamp = (/* @__PURE__ */ new Date()).toISOString().slice(11, 19).replace(/:/g, "");
    return `ts_${v4_default().slice(0, 8)}_${timestamp}`;
  }
  /**
   * Get logs for AI analysis (formatted for Claude Code)
   * Output directory: {project_root}/uLoopMCPOutputs/VibeLogs/
   */
  static getLogsForAi(operation, correlationId, maxCount = 100) {
    let filteredLogs = [..._VibeLogger.memoryLogs];
    if (operation) {
      filteredLogs = filteredLogs.filter((log) => log.operation.includes(operation));
    }
    if (correlationId) {
      filteredLogs = filteredLogs.filter((log) => log.correlation_id === correlationId);
    }
    if (filteredLogs.length > maxCount) {
      filteredLogs = filteredLogs.slice(-maxCount);
    }
    return JSON.stringify(filteredLogs, null, 2);
  }
  /**
   * Clear all memory logs
   */
  static clearMemoryLogs() {
    _VibeLogger.memoryLogs = [];
  }
  /**
   * Write emergency log entry (for when main logging fails)
   * Static method to avoid circular dependency
   */
  static writeEmergencyLog(emergencyEntry) {
    try {
      const basePath = process.cwd();
      const sanitizedRoot = path.resolve(basePath, OUTPUT_DIRECTORIES.ROOT);
      if (!_VibeLogger.validateWithin(basePath, sanitizedRoot)) {
        return;
      }
      const emergencyLogDir = path.resolve(sanitizedRoot, "EmergencyLogs");
      if (!_VibeLogger.validateWithin(sanitizedRoot, emergencyLogDir)) {
        return;
      }
      fs.mkdirSync(emergencyLogDir, { recursive: true });
      const emergencyLogPath = path.resolve(emergencyLogDir, "vibe-logger-emergency.log");
      if (!_VibeLogger.validateWithin(emergencyLogDir, emergencyLogPath)) {
        return;
      }
      const emergencyLog = JSON.stringify(emergencyEntry) + "\n";
      fs.appendFileSync(emergencyLogPath, emergencyLog);
    } catch (error) {
    }
  }
  /**
   * Core logging method
   * Only logs when MCP_DEBUG environment variable is set to 'true'
   */
  static log(level, operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace) {
    if (!_VibeLogger.isDebugEnabled) {
      return;
    }
    let finalContext = context;
    if (includeStackTrace) {
      finalContext = {
        original_context: context,
        stack: _VibeLogger.cleanStackTrace(new Error().stack)
      };
    }
    const logEntry = {
      timestamp: _VibeLogger.formatTimestamp(),
      level,
      operation,
      message,
      context: _VibeLogger.sanitizeContext(finalContext),
      correlation_id: correlationId || _VibeLogger.generateCorrelationId(),
      source: "TypeScript",
      human_note: humanNote,
      ai_todo: aiTodo,
      environment: _VibeLogger.getEnvironmentInfo()
    };
    _VibeLogger.memoryLogs.push(logEntry);
    if (_VibeLogger.memoryLogs.length > _VibeLogger.MAX_MEMORY_LOGS) {
      _VibeLogger.memoryLogs.shift();
    }
    _VibeLogger.saveLogToFile(logEntry).catch((error) => {
      _VibeLogger.writeEmergencyLog({
        timestamp: _VibeLogger.formatTimestamp(),
        level: "EMERGENCY",
        message: "VibeLogger saveLogToFile failed",
        original_error: error instanceof Error ? error.message : String(error),
        original_log_entry: logEntry
      });
    });
  }
  /**
   * Validate file name to prevent dangerous characters
   */
  static validateFileName(fileName) {
    const safeFileNameRegex = /^[a-zA-Z0-9._-]+$/;
    return safeFileNameRegex.test(fileName) && !fileName.includes("..");
  }
  /**
   * Validate file path to prevent directory traversal attacks
   */
  static validateFilePath(filePath) {
    const normalizedPath = path.normalize(filePath);
    const logDirectory = path.normalize(_VibeLogger.LOG_DIRECTORY);
    return normalizedPath.startsWith(logDirectory + path.sep) || normalizedPath === logDirectory;
  }
  /**
   * Validate that target path is within base directory
   */
  static validateWithin(base, target) {
    const resolvedBase = path.resolve(base);
    const resolvedTarget = path.resolve(target);
    return resolvedTarget.startsWith(resolvedBase);
  }
  /**
   * Safe wrapper for fs.existsSync with path validation
   */
  static safeExistsSync(filePath) {
    try {
      const absolutePath = path.resolve(filePath);
      const expectedDir = path.resolve(_VibeLogger.LOG_DIRECTORY);
      if (!_VibeLogger.validateWithin(expectedDir, absolutePath)) {
        return false;
      }
      return fs.existsSync(absolutePath);
    } catch (error) {
      return false;
    }
  }
  /**
   * Prepare and validate log directory and file path
   */
  static prepareLogFilePath() {
    const logDirectory = path.normalize(_VibeLogger.LOG_DIRECTORY);
    if (!_VibeLogger.validateFilePath(logDirectory)) {
      throw new Error("Invalid log directory path");
    }
    if (!_VibeLogger.safeExistsSync(logDirectory)) {
      const absoluteLogDir = path.resolve(logDirectory);
      const expectedBaseDir = path.resolve(_VibeLogger.PROJECT_ROOT);
      if (!_VibeLogger.validateWithin(expectedBaseDir, absoluteLogDir)) {
        throw new Error("Log directory path escapes project root");
      }
      fs.mkdirSync(absoluteLogDir, { recursive: true });
    }
    const fileName = `${_VibeLogger.LOG_FILE_PREFIX}_${_VibeLogger.formatDate()}.json`;
    if (!_VibeLogger.validateFileName(fileName)) {
      throw new Error("Invalid file name detected");
    }
    const filePath = path.resolve(logDirectory, fileName);
    if (!_VibeLogger.validateWithin(logDirectory, filePath)) {
      throw new Error("Resolved file path escapes the log directory");
    }
    if (!_VibeLogger.validateFilePath(filePath)) {
      throw new Error("Invalid file path detected");
    }
    return filePath;
  }
  /**
   * Rotate log file if it exceeds maximum size
   */
  static rotateLogFileIfNeeded(filePath) {
    if (!_VibeLogger.safeExistsSync(filePath)) {
      return;
    }
    const absoluteFilePath = path.resolve(filePath);
    const stats = fs.statSync(absoluteFilePath);
    if (stats.size <= _VibeLogger.MAX_FILE_SIZE_MB * 1024 * 1024) {
      return;
    }
    const logDirectory = path.dirname(filePath);
    const rotatedFileName = `${_VibeLogger.LOG_FILE_PREFIX}_${_VibeLogger.formatDateTime()}.json`;
    if (!_VibeLogger.validateFileName(rotatedFileName)) {
      throw new Error("Invalid rotated file name detected");
    }
    const rotatedFilePath = path.resolve(logDirectory, rotatedFileName);
    if (!_VibeLogger.validateWithin(logDirectory, rotatedFilePath)) {
      throw new Error("Rotated file path escapes the allowed directory");
    }
    const sanitizedFilePath = path.resolve(logDirectory, path.basename(filePath));
    if (!_VibeLogger.validateWithin(logDirectory, sanitizedFilePath)) {
      throw new Error("Original file path escapes the allowed directory");
    }
    fs.renameSync(sanitizedFilePath, rotatedFilePath);
  }
  /**
   * Write log to file with retry mechanism for concurrent access
   */
  static async writeLogWithRetry(filePath, jsonLog) {
    const maxRetries = 3;
    const baseDelayMs = 50;
    const absoluteFilePath = path.resolve(filePath);
    const expectedLogDir = path.resolve(_VibeLogger.LOG_DIRECTORY);
    if (!_VibeLogger.validateWithin(expectedLogDir, absoluteFilePath)) {
      throw new Error("File path escapes log directory");
    }
    for (let retry = 0; retry < maxRetries; retry++) {
      try {
        const fileHandle = await fs.promises.open(absoluteFilePath, "a");
        try {
          await fileHandle.writeFile(jsonLog, { encoding: "utf8" });
        } finally {
          await fileHandle.close();
        }
        return;
      } catch (error) {
        if (_VibeLogger.isFileSharingViolation(error) && retry < maxRetries - 1) {
          const delayMs = baseDelayMs * Math.pow(2, retry);
          await _VibeLogger.sleep(delayMs);
        } else {
          throw error;
        }
      }
    }
  }
  /**
   * Save log entry to file with retry mechanism for concurrent access
   */
  static async saveLogToFile(logEntry) {
    try {
      const filePath = _VibeLogger.prepareLogFilePath();
      _VibeLogger.rotateLogFileIfNeeded(filePath);
      const jsonLog = JSON.stringify(logEntry) + "\n";
      await _VibeLogger.writeLogWithRetry(filePath, jsonLog);
    } catch (error) {
      await _VibeLogger.tryFallbackLogging(logEntry, error);
    }
  }
  /**
   * Try fallback logging when main logging fails
   */
  static async tryFallbackLogging(logEntry, error) {
    try {
      const basePath = process.cwd();
      const sanitizedRoot = path.resolve(basePath, OUTPUT_DIRECTORIES.ROOT);
      if (!_VibeLogger.validateWithin(basePath, sanitizedRoot)) {
        throw new Error("Invalid OUTPUT_DIRECTORIES.ROOT path");
      }
      const safeLogDir = path.resolve(sanitizedRoot, "FallbackLogs");
      if (!_VibeLogger.validateWithin(sanitizedRoot, safeLogDir)) {
        throw new Error("Fallback log directory path traversal detected");
      }
      fs.mkdirSync(safeLogDir, { recursive: true });
      const safeDate = _VibeLogger.formatDateTime().split(" ")[0].replace(/[^0-9-]/g, "");
      const safeFilename = `${_VibeLogger.LOG_FILE_PREFIX}_fallback_${safeDate}.json`;
      const safeFallbackPath = path.resolve(safeLogDir, safeFilename);
      if (!_VibeLogger.validateWithin(safeLogDir, safeFallbackPath)) {
        throw new Error("Invalid fallback log file path");
      }
      const fallbackEntry = {
        ...logEntry,
        fallback_reason: error instanceof Error ? error.message : String(error),
        original_timestamp: logEntry.timestamp
      };
      const jsonLog = JSON.stringify(fallbackEntry) + "\n";
      const fileHandle = await fs.promises.open(safeFallbackPath, "a");
      try {
        await fileHandle.writeFile(jsonLog, { encoding: "utf8" });
      } finally {
        await fileHandle.close();
      }
    } catch (fallbackError) {
      _VibeLogger.writeEmergencyLog({
        timestamp: _VibeLogger.formatTimestamp(),
        level: "EMERGENCY",
        message: "VibeLogger fallback failed",
        original_error: error instanceof Error ? error.message : String(error),
        fallback_error: fallbackError instanceof Error ? fallbackError.message : String(fallbackError),
        original_log_entry: logEntry
      });
    }
  }
  /**
   * Check if error is a file sharing violation
   */
  static isFileSharingViolation(error) {
    if (!error) {
      return false;
    }
    const sharingViolationCodes = [
      "EBUSY",
      // Resource busy or locked
      "EACCES",
      // Permission denied (can indicate file in use)
      "EPERM",
      // Operation not permitted
      "EMFILE",
      // Too many open files
      "ENFILE"
      // File table overflow
    ];
    return error && typeof error === "object" && "code" in error && typeof error.code === "string" && sharingViolationCodes.includes(error.code);
  }
  /**
   * Asynchronous sleep function for retry delays
   */
  static async sleep(ms) {
    return new Promise((resolve2) => setTimeout(resolve2, ms));
  }
  /**
   * Get current environment information
   */
  static getEnvironmentInfo() {
    const memUsage = process.memoryUsage();
    return {
      node_version: process.version,
      platform: process.platform,
      process_id: process.pid,
      memory_usage: {
        rss: memUsage.rss,
        heapTotal: memUsage.heapTotal,
        heapUsed: memUsage.heapUsed
      }
    };
  }
  /**
   * Sanitize context to prevent circular references
   */
  static sanitizeContext(context) {
    if (!context) {
      return void 0;
    }
    try {
      return JSON.parse(JSON.stringify(context));
    } catch (error) {
      return {
        error: "Failed to serialize context",
        original_type: typeof context,
        circular_reference: true
      };
    }
  }
  /**
   * Format timestamp for log entries (local timezone)
   */
  static formatTimestamp() {
    const now = /* @__PURE__ */ new Date();
    const offset = now.getTimezoneOffset();
    const offsetHours = String(Math.floor(Math.abs(offset) / 60)).padStart(2, "0");
    const offsetMinutes = String(Math.abs(offset) % 60).padStart(2, "0");
    const offsetSign = offset <= 0 ? "+" : "-";
    return now.toISOString().replace("Z", `${offsetSign}${offsetHours}:${offsetMinutes}`);
  }
  /**
   * Format date for file naming (local timezone)
   */
  static formatDate() {
    const now = /* @__PURE__ */ new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return `${year}${month}${day}`;
  }
  /**
   * Format datetime for file rotation
   */
  static formatDateTime() {
    const now = /* @__PURE__ */ new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    const hours = String(now.getHours()).padStart(2, "0");
    const minutes = String(now.getMinutes()).padStart(2, "0");
    const seconds = String(now.getSeconds()).padStart(2, "0");
    return `${year}${month}${day}_${hours}${minutes}${seconds}`;
  }
  /**
   * Clean stack trace by removing VibeLogger internal calls and Error prefix
   */
  static cleanStackTrace(stack) {
    if (!stack) {
      return "";
    }
    const lines = stack.split("\n");
    const cleanedLines = [];
    for (const line of lines) {
      if (line.trim() === "Error") {
        continue;
      }
      if (line.includes("vibe-logger.ts") || line.includes("VibeLogger")) {
        continue;
      }
      cleanedLines.push(line);
    }
    return cleanedLines.join("\n").trim();
  }
};

// src/connection-manager.ts
var ConnectionManager = class {
  onReconnectedCallback = null;
  onConnectionLostCallback = null;
  /**
   * Set callback for when connection is restored
   */
  setReconnectedCallback(callback) {
    this.onReconnectedCallback = callback;
  }
  /**
   * Set callback for when connection is lost
   */
  setConnectionLostCallback(callback) {
    this.onConnectionLostCallback = callback;
  }
  /**
   * Trigger reconnection callback
   */
  triggerReconnected() {
    if (this.onReconnectedCallback) {
      try {
        this.onReconnectedCallback();
      } catch (error) {
        VibeLogger.logError(
          "connection_manager_reconnect_error",
          "Error in reconnection callback",
          { error }
        );
      }
    }
  }
  /**
   * Trigger connection lost callback
   */
  triggerConnectionLost() {
    if (this.onConnectionLostCallback) {
      try {
        this.onConnectionLostCallback();
      } catch (error) {
        VibeLogger.logError(
          "connection_manager_connection_lost_error",
          "Error in connection lost callback",
          { error }
        );
      }
    }
  }
  /**
   * Legacy method for backward compatibility - now does nothing
   * @deprecated Use UnityDiscovery for connection polling instead
   */
  startPolling(_connectFn) {
  }
  /**
   * Legacy method for backward compatibility - now does nothing
   * @deprecated Polling is now handled by UnityDiscovery
   */
  stopPolling() {
  }
};

// src/utils/content-length-framer.ts
var ContentLengthFramer = class _ContentLengthFramer {
  static CONTENT_LENGTH_HEADER = "Content-Length:";
  static HEADER_SEPARATOR = "\r\n\r\n";
  static LINE_SEPARATOR = "\r\n";
  static MAX_MESSAGE_SIZE = 1024 * 1024;
  // 1MB
  static ENCODING_UTF8 = "utf8";
  static LOG_PREFIX = "[ContentLengthFramer]";
  static ERROR_MESSAGE_JSON_EMPTY = "JSON content cannot be empty";
  static DEBUG_PREVIEW_LENGTH = 50;
  static PREVIEW_SUFFIX = "...";
  /**
   * Creates a Content-Length framed message from JSON content.
   * @param jsonContent The JSON content to frame
   * @returns The framed message with Content-Length header
   */
  static createFrame(jsonContent) {
    if (!jsonContent) {
      throw new Error(_ContentLengthFramer.ERROR_MESSAGE_JSON_EMPTY);
    }
    const contentLength = Buffer.byteLength(jsonContent, _ContentLengthFramer.ENCODING_UTF8);
    if (contentLength > _ContentLengthFramer.MAX_MESSAGE_SIZE) {
      throw new Error(
        `Message size ${contentLength} exceeds maximum allowed size ${_ContentLengthFramer.MAX_MESSAGE_SIZE}`
      );
    }
    return `${_ContentLengthFramer.CONTENT_LENGTH_HEADER} ${contentLength}${_ContentLengthFramer.HEADER_SEPARATOR}${jsonContent}`;
  }
  /**
   * Parses a Content-Length header from incoming data.
   * @param data The incoming data string
   * @returns Parse result with content length, header length, and completion status
   */
  static parseFrame(data) {
    if (!data) {
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false
      };
    }
    try {
      const separatorIndex = data.indexOf(_ContentLengthFramer.HEADER_SEPARATOR);
      if (separatorIndex === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false
        };
      }
      const headerSection = data.substring(0, separatorIndex);
      const headerLength = separatorIndex + _ContentLengthFramer.HEADER_SEPARATOR.length;
      const contentLength = _ContentLengthFramer.parseContentLength(headerSection);
      if (contentLength === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false
        };
      }
      const expectedTotalLength = headerLength + contentLength;
      const actualByteLength = Buffer.byteLength(data, _ContentLengthFramer.ENCODING_UTF8);
      const isComplete = actualByteLength >= expectedTotalLength;
      VibeLogger.logDebug(
        "frame_analysis",
        `Frame analysis: dataLength=${data.length}, actualByteLength=${actualByteLength}, contentLength=${contentLength}, headerLength=${headerLength}, expectedTotal=${expectedTotalLength}, isComplete=${isComplete}`,
        {
          dataLength: data.length,
          actualByteLength,
          contentLength,
          headerLength,
          expectedTotalLength,
          isComplete
        }
      );
      return {
        contentLength,
        headerLength,
        isComplete
      };
    } catch (error) {
      VibeLogger.logError(
        "parse_frame_error",
        `Error parsing frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error: error instanceof Error ? {
            name: error.name,
            message: error.message,
            stack: error.stack
          } : { raw: String(error) }
        }
      );
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false
      };
    }
  }
  /**
   * Parses a Content-Length header from incoming Buffer data.
   * @param data The incoming data Buffer
   * @returns Parse result with content length, header length, and completion status
   */
  static parseFrameFromBuffer(data) {
    if (!data || data.length === 0) {
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false
      };
    }
    try {
      const separatorBuffer = Buffer.from(
        _ContentLengthFramer.HEADER_SEPARATOR,
        _ContentLengthFramer.ENCODING_UTF8
      );
      const separatorIndex = data.indexOf(separatorBuffer);
      if (separatorIndex === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false
        };
      }
      const headerSection = data.subarray(0, separatorIndex).toString(_ContentLengthFramer.ENCODING_UTF8);
      const headerLength = separatorIndex + separatorBuffer.length;
      const contentLength = _ContentLengthFramer.parseContentLength(headerSection);
      if (contentLength === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false
        };
      }
      const expectedTotalLength = headerLength + contentLength;
      const actualByteLength = data.length;
      const isComplete = actualByteLength >= expectedTotalLength;
      VibeLogger.logDebug(
        "frame_analysis_buffer",
        `Frame analysis: dataLength=${data.length}, actualByteLength=${actualByteLength}, contentLength=${contentLength}, headerLength=${headerLength}, expectedTotal=${expectedTotalLength}, isComplete=${isComplete}`,
        {
          dataLength: data.length,
          actualByteLength,
          contentLength,
          headerLength,
          expectedTotalLength,
          isComplete
        }
      );
      return {
        contentLength,
        headerLength,
        isComplete
      };
    } catch (error) {
      VibeLogger.logError(
        "parse_frame_buffer_error",
        `Error parsing frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error: error instanceof Error ? {
            name: error.name,
            message: error.message,
            stack: error.stack
          } : { raw: String(error) }
        }
      );
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false
      };
    }
  }
  /**
   * Extracts a complete frame from the data buffer.
   * @param data The data buffer containing the frame
   * @param contentLength The expected content length
   * @param headerLength The header length including separators
   * @returns The extracted JSON content and remaining data
   */
  static extractFrame(data, contentLength, headerLength) {
    if (!data || contentLength < 0 || headerLength < 0) {
      return {
        jsonContent: null,
        remainingData: data || ""
      };
    }
    try {
      const expectedTotalLength = headerLength + contentLength;
      const actualByteLength = Buffer.byteLength(data, _ContentLengthFramer.ENCODING_UTF8);
      if (actualByteLength < expectedTotalLength) {
        return {
          jsonContent: null,
          remainingData: data
        };
      }
      const dataBuffer = Buffer.from(data, _ContentLengthFramer.ENCODING_UTF8);
      const jsonContent = dataBuffer.subarray(headerLength, headerLength + contentLength).toString(_ContentLengthFramer.ENCODING_UTF8);
      const remainingData = dataBuffer.subarray(expectedTotalLength).toString(_ContentLengthFramer.ENCODING_UTF8);
      return {
        jsonContent,
        remainingData
      };
    } catch (error) {
      VibeLogger.logError(
        "extract_frame_error",
        `Error extracting frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error: error instanceof Error ? {
            name: error.name,
            message: error.message,
            stack: error.stack
          } : { raw: String(error) }
        }
      );
      return {
        jsonContent: null,
        remainingData: data
      };
    }
  }
  /**
   * Extracts a complete frame from the Buffer data.
   * @param data The Buffer containing the frame
   * @param contentLength The expected content length
   * @param headerLength The header length including separators
   * @returns The extracted JSON content and remaining data
   */
  static extractFrameFromBuffer(data, contentLength, headerLength) {
    if (!data || data.length === 0 || contentLength < 0 || headerLength < 0) {
      return {
        jsonContent: null,
        remainingData: data || Buffer.alloc(0)
      };
    }
    try {
      const expectedTotalLength = headerLength + contentLength;
      if (data.length < expectedTotalLength) {
        return {
          jsonContent: null,
          remainingData: data
        };
      }
      const jsonContent = data.subarray(headerLength, headerLength + contentLength).toString(_ContentLengthFramer.ENCODING_UTF8);
      const remainingData = data.subarray(expectedTotalLength);
      return {
        jsonContent,
        remainingData
      };
    } catch (error) {
      VibeLogger.logError(
        "extract_frame_buffer_error",
        `Error extracting frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error: error instanceof Error ? {
            name: error.name,
            message: error.message,
            stack: error.stack
          } : { raw: String(error) }
        }
      );
      return {
        jsonContent: null,
        remainingData: data
      };
    }
  }
  /**
   * Validates that the Content-Length value is within acceptable limits.
   * @param contentLength The content length to validate
   * @returns True if the content length is valid, false otherwise
   */
  static isValidContentLength(contentLength) {
    return contentLength >= 0 && contentLength <= _ContentLengthFramer.MAX_MESSAGE_SIZE;
  }
  /**
   * Parses the Content-Length value from the header section.
   * @param headerSection The header section string
   * @returns The parsed content length, or -1 if parsing failed
   */
  static parseContentLength(headerSection) {
    if (!headerSection) {
      return -1;
    }
    const lines = headerSection.split(/\r?\n/);
    for (const line of lines) {
      const trimmedLine = line.trim();
      const lowerLine = trimmedLine.toLowerCase();
      if (lowerLine.startsWith("content-length:")) {
        const colonIndex = trimmedLine.indexOf(":");
        if (colonIndex === -1 || colonIndex >= trimmedLine.length - 1) {
          continue;
        }
        const valueString = trimmedLine.substring(colonIndex + 1).trim();
        const parsedValue = parseInt(valueString, 10);
        if (isNaN(parsedValue)) {
          return -1;
        }
        if (!_ContentLengthFramer.isValidContentLength(parsedValue)) {
          return -1;
        }
        return parsedValue;
      }
    }
    return -1;
  }
};

// src/utils/dynamic-buffer.ts
var DynamicBuffer = class _DynamicBuffer {
  // 定数定義
  static ENCODING_UTF8 = "utf8";
  static HEADER_SEPARATOR = "\r\n\r\n";
  static CONTENT_LENGTH_HEADER = "content-length:";
  static LOG_PREFIX = "[DynamicBuffer]";
  static PREVIEW_SUFFIX = "...";
  static PREVIEW_LENGTH_FRAME_ERROR = 200;
  static LARGE_BUFFER_THRESHOLD = 1024;
  static BUFFER_UTILIZATION_THRESHOLD = 0.8;
  static DEFAULT_PREVIEW_LENGTH = 100;
  static STATS_PREVIEW_LENGTH = 50;
  buffer = Buffer.alloc(0);
  maxBufferSize;
  initialBufferSize;
  constructor(maxBufferSize = 1024 * 1024, initialBufferSize = 4096) {
    this.maxBufferSize = maxBufferSize;
    this.initialBufferSize = initialBufferSize;
  }
  /**
   * Appends new data to the buffer.
   * @param data The data to append (Buffer or string)
   * @throws Error if buffer would exceed maximum size
   */
  append(data) {
    if (!data) {
      return;
    }
    const dataBuffer = Buffer.isBuffer(data) ? data : Buffer.from(data, _DynamicBuffer.ENCODING_UTF8);
    const newSize = this.buffer.length + dataBuffer.length;
    if (newSize > this.maxBufferSize) {
      throw new Error(
        `Buffer size would exceed maximum allowed size: ${newSize} > ${this.maxBufferSize}`
      );
    }
    this.buffer = Buffer.concat([this.buffer, dataBuffer]);
  }
  /**
   * Attempts to extract a complete frame from the buffer.
   * @returns The extracted frame and whether extraction was successful
   */
  extractFrame() {
    if (!this.buffer) {
      return { frame: null, extracted: false };
    }
    try {
      const parseResult = ContentLengthFramer.parseFrameFromBuffer(this.buffer);
      if (!parseResult.isComplete) {
        return { frame: null, extracted: false };
      }
      const extractionResult = ContentLengthFramer.extractFrameFromBuffer(
        this.buffer,
        parseResult.contentLength,
        parseResult.headerLength
      );
      if (!extractionResult.jsonContent) {
        return { frame: null, extracted: false };
      }
      this.buffer = Buffer.isBuffer(extractionResult.remainingData) ? extractionResult.remainingData : Buffer.from(extractionResult.remainingData, _DynamicBuffer.ENCODING_UTF8);
      return {
        frame: extractionResult.jsonContent,
        extracted: true
      };
    } catch (error) {
      return { frame: null, extracted: false };
    }
  }
  /**
   * Extracts all available complete frames from the buffer.
   * @returns Array of extracted JSON frames
   */
  extractAllFrames() {
    const frames = [];
    while (this.buffer.length > 0) {
      const result = this.extractFrame();
      if (!result.extracted || !result.frame) {
        break;
      }
      frames.push(result.frame);
    }
    return frames;
  }
  /**
   * Checks if the buffer has any data.
   * @returns True if buffer contains data, false otherwise
   */
  hasData() {
    return this.buffer.length > 0;
  }
  /**
   * Gets the current buffer size.
   * @returns The current buffer size in characters
   */
  getSize() {
    return this.buffer.length;
  }
  /**
   * Checks if a complete frame might be available.
   * This is a quick check without full parsing.
   * @returns True if header separator is found, false otherwise
   */
  hasCompleteFrameHeader() {
    return this.buffer.includes(
      Buffer.from(_DynamicBuffer.HEADER_SEPARATOR, _DynamicBuffer.ENCODING_UTF8)
    );
  }
  /**
   * Clears the buffer.
   */
  clear() {
    this.buffer = Buffer.alloc(0);
  }
  /**
   * Gets a preview of the buffer content for debugging.
   * @param maxLength Maximum length of preview (default: 100)
   * @returns Truncated buffer content for debugging
   */
  getPreview(maxLength = _DynamicBuffer.DEFAULT_PREVIEW_LENGTH) {
    if (this.buffer.length <= maxLength) {
      return this.buffer.toString(_DynamicBuffer.ENCODING_UTF8);
    }
    return this.buffer.subarray(0, maxLength).toString(_DynamicBuffer.ENCODING_UTF8) + _DynamicBuffer.PREVIEW_SUFFIX;
  }
  /**
   * Validates the buffer state and performs cleanup if necessary.
   * @returns True if buffer is in valid state, false if cleanup was performed
   */
  validateAndCleanup() {
    if (this.buffer.length > this.maxBufferSize * _DynamicBuffer.BUFFER_UTILIZATION_THRESHOLD && !this.hasCompleteFrameHeader()) {
      this.clear();
      return false;
    }
    const headerSeparatorBuffer = Buffer.from(
      _DynamicBuffer.HEADER_SEPARATOR,
      _DynamicBuffer.ENCODING_UTF8
    );
    const headerSeparatorIndex = this.buffer.indexOf(headerSeparatorBuffer);
    if (headerSeparatorIndex === -1 && this.buffer.length > _DynamicBuffer.LARGE_BUFFER_THRESHOLD) {
      const contentLengthBuffer = Buffer.from(
        _DynamicBuffer.CONTENT_LENGTH_HEADER,
        _DynamicBuffer.ENCODING_UTF8
      );
      const contentLengthIndex = this.buffer.indexOf(contentLengthBuffer);
      if (contentLengthIndex === -1) {
        this.clear();
        return false;
      }
    }
    return true;
  }
  /**
   * Gets buffer statistics for monitoring and debugging.
   * @returns Buffer statistics object
   */
  getStats() {
    return {
      size: this.buffer.length,
      maxSize: this.maxBufferSize,
      utilization: this.buffer.length / this.maxBufferSize * 100,
      hasCompleteHeader: this.hasCompleteFrameHeader(),
      preview: this.getPreview(_DynamicBuffer.STATS_PREVIEW_LENGTH)
    };
  }
};

// src/message-handler.ts
var JsonRpcErrorTypes = {
  SECURITY_BLOCKED: "security_blocked",
  INTERNAL_ERROR: "internal_error"
};
var isJsonRpcNotification = (msg) => {
  return typeof msg === "object" && msg !== null && "method" in msg && typeof msg.method === "string" && !("id" in msg);
};
var isJsonRpcResponse = (msg) => {
  return typeof msg === "object" && msg !== null && "id" in msg && typeof msg.id === "string" && !("method" in msg);
};
var hasValidId = (msg) => {
  return typeof msg === "object" && msg !== null && "id" in msg && typeof msg.id === "string";
};
var MessageHandler = class {
  notificationHandlers = /* @__PURE__ */ new Map();
  pendingRequests = /* @__PURE__ */ new Map();
  // Content-Length framing components
  dynamicBuffer = new DynamicBuffer();
  /**
   * Register notification handler for specific method
   */
  onNotification(method, handler) {
    this.notificationHandlers.set(method, handler);
  }
  /**
   * Remove notification handler
   */
  offNotification(method) {
    this.notificationHandlers.delete(method);
  }
  /**
   * Register a pending request
   */
  registerPendingRequest(id, resolve2, reject) {
    this.pendingRequests.set(id, { resolve: resolve2, reject, timestamp: Date.now() });
  }
  /**
   * Handle incoming data from Unity using Content-Length framing
   */
  handleIncomingData(data) {
    this.dynamicBuffer.append(data);
    const frames = this.dynamicBuffer.extractAllFrames();
    for (const frame of frames) {
      if (!frame || frame.trim() === "") {
        continue;
      }
      try {
        const message = JSON.parse(frame);
        if (isJsonRpcNotification(message)) {
          this.handleNotification(message);
        } else if (isJsonRpcResponse(message)) {
          this.handleResponse(message);
        } else if (hasValidId(message)) {
          this.handleResponse(message);
        }
      } catch (parseError) {
        VibeLogger.logError(
          "json_parse_error",
          "Error parsing JSON frame",
          {
            error: parseError instanceof Error ? parseError.message : String(parseError),
            frame: frame.substring(0, 200)
            // Truncate for security
          },
          void 0,
          "Invalid JSON received from Unity, frame may be corrupted"
        );
      }
    }
  }
  /**
   * Handle notification from Unity
   */
  handleNotification(notification) {
    const { method, params } = notification;
    const handler = this.notificationHandlers.get(method);
    if (handler) {
      try {
        handler(params);
      } catch (error) {
        VibeLogger.logError(
          "notification_handler_error",
          `Error in notification handler for ${method}`,
          { error: error instanceof Error ? error.message : String(error) },
          void 0,
          "Exception occurred while processing notification"
        );
      }
    }
  }
  /**
   * Handle response from Unity
   */
  handleResponse(response) {
    const { id } = response;
    const pending = this.pendingRequests.get(id);
    if (pending) {
      this.pendingRequests.delete(id);
      if (response.error) {
        let errorMessage = response.error.message || "Unknown error";
        if (response.error.data?.type === JsonRpcErrorTypes.SECURITY_BLOCKED) {
          const data = response.error.data;
          errorMessage = `${data.reason || errorMessage}`;
          if (data.command) {
            errorMessage += ` (Command: ${data.command})`;
          }
          errorMessage += " To use this feature, enable the corresponding option in Unity menu: Window > uLoopMCP > Security Settings";
        }
        pending.reject(new Error(errorMessage));
      } else {
        pending.resolve(response);
      }
    } else {
      const activeRequestIds = Array.from(this.pendingRequests.keys()).join(", ");
      const currentTime = Date.now();
      VibeLogger.logWarning(
        "unknown_request_response",
        `Received response for unknown request ID: ${id}`,
        {
          unknown_request_id: id,
          active_request_ids: activeRequestIds,
          current_time: currentTime
        },
        void 0,
        "This may be a delayed response from before reconnection",
        "Monitor if this pattern indicates connection stability issues"
      );
    }
  }
  /**
   * Clear all pending requests (used during disconnect)
   */
  clearPendingRequests(reason) {
    for (const [, pending] of this.pendingRequests) {
      pending.reject(new Error(reason));
    }
    this.pendingRequests.clear();
  }
  /**
   * Create JSON-RPC request with Content-Length framing
   */
  createRequest(method, params, id) {
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id,
      method,
      params
    };
    const jsonContent = JSON.stringify(request);
    return ContentLengthFramer.createFrame(jsonContent);
  }
  /**
   * Clear the dynamic buffer (for connection reset)
   */
  clearBuffer() {
    this.dynamicBuffer.clear();
  }
  /**
   * Get buffer statistics for debugging
   */
  getBufferStats() {
    return this.dynamicBuffer.getStats();
  }
};

// src/unity-client.ts
var createSafeTimeout = (callback, delay) => {
  return safeSetTimeout(callback, delay);
};
var UnityClient = class _UnityClient {
  static MAX_COUNTER = 9999;
  static COUNTER_PADDING = 4;
  static instance = null;
  socket = null;
  _connected = false;
  port;
  host = UNITY_CONNECTION.DEFAULT_HOST;
  reconnectHandlers = /* @__PURE__ */ new Set();
  connectionManager = new ConnectionManager();
  messageHandler = new MessageHandler();
  unityDiscovery = null;
  // Reference to UnityDiscovery for connection loss handling
  requestIdCounter = 0;
  // Will be incremented to 1 on first use
  processId = process.pid;
  randomSeed = Math.floor(Math.random() * 1e3);
  storedClientName = null;
  isConnecting = false;
  connectingPromise = null;
  constructor() {
    const unityTcpPort = process.env.UNITY_TCP_PORT;
    if (!unityTcpPort) {
      throw new Error("UNITY_TCP_PORT environment variable is required but not set");
    }
    const parsedPort = parseInt(unityTcpPort, 10);
    if (isNaN(parsedPort) || parsedPort <= 0 || parsedPort > 65535) {
      throw new Error(`UNITY_TCP_PORT must be a valid port number (1-65535), got: ${unityTcpPort}`);
    }
    this.port = parsedPort;
  }
  /**
   * Get the singleton instance of UnityClient
   */
  static getInstance() {
    if (!_UnityClient.instance) {
      _UnityClient.instance = new _UnityClient();
    }
    return _UnityClient.instance;
  }
  /**
   * Reset the singleton instance (for testing purposes)
   */
  static resetInstance() {
    if (_UnityClient.instance) {
      _UnityClient.instance.disconnect();
      _UnityClient.instance.storedClientName = null;
      _UnityClient.instance = null;
    }
  }
  /**
   * Update Unity connection port (for discovery)
   */
  updatePort(newPort) {
    this.port = newPort;
  }
  /**
   * Set Unity Discovery reference for connection loss handling
   */
  setUnityDiscovery(unityDiscovery) {
    this.unityDiscovery = unityDiscovery;
  }
  get connected() {
    return this._connected && this.socket !== null && !this.socket.destroyed;
  }
  /**
   * Register notification handler for specific method
   */
  onNotification(method, handler) {
    this.messageHandler.onNotification(method, handler);
  }
  /**
   * Remove notification handler
   */
  offNotification(method) {
    this.messageHandler.offNotification(method);
  }
  /**
   * Register reconnect handler
   */
  onReconnect(handler) {
    this.reconnectHandlers.add(handler);
  }
  /**
   * Remove reconnect handler
   */
  offReconnect(handler) {
    this.reconnectHandlers.delete(handler);
  }
  /**
   * Lightweight connection health check
   * Tests socket state without creating new connections
   */
  async testConnection() {
    if (!this._connected || this.socket === null || this.socket.destroyed) {
      return false;
    }
    if (!this.socket.readable || !this.socket.writable) {
      this._connected = false;
      return false;
    }
    let timeoutTimer = null;
    try {
      await Promise.race([
        this.ping(UNITY_CONNECTION.CONNECTION_TEST_MESSAGE),
        new Promise((_resolve, reject) => {
          timeoutTimer = createSafeTimeout(() => {
            reject(new Error("Health check timeout"));
          }, 1e3);
        })
      ]);
    } catch (error) {
      return false;
    } finally {
      stopSafeTimer(timeoutTimer);
      timeoutTimer = null;
    }
    return true;
  }
  /**
   * Ensure connection to Unity (singleton-safe reconnection)
   * Properly manages single connection instance
   */
  async ensureConnected() {
    if (this._connected && this.socket && !this.socket.destroyed) {
      try {
        if (await this.testConnection()) {
          return;
        }
      } catch (error) {
      }
    }
    if (this.connectingPromise) {
      await this.connectingPromise;
      return;
    }
    this.connectingPromise = (async () => {
      this.isConnecting = true;
      this.disconnect();
      await this.connect();
    })().catch((error) => {
      VibeLogger.logError("unity_connect_failed", "Unity connect attempt failed", {
        message: error instanceof Error ? error.message : String(error)
      });
      throw error;
    }).finally(() => {
      this.isConnecting = false;
      this.connectingPromise = null;
    });
    await this.connectingPromise;
  }
  /**
   * Connect to Unity
   * Creates a new socket connection (should only be called after disconnect)
   */
  async connect() {
    if (this._connected && this.socket && !this.socket.destroyed) {
      return;
    }
    return new Promise((resolve2, reject) => {
      this.socket = new net.Socket();
      const currentSocket = this.socket;
      let connectionEstablished = false;
      let promiseSettled = false;
      const finalizeInitialFailure = (error, logCode, logMessage) => {
        if (promiseSettled) {
          return;
        }
        promiseSettled = true;
        VibeLogger.logError(logCode, logMessage, { message: error.message });
        if (!currentSocket.destroyed) {
          currentSocket.destroy();
        }
        if (this.socket === currentSocket) {
          this.socket = null;
        }
        reject(error);
      };
      currentSocket.connect(this.port, this.host, () => {
        this._connected = true;
        connectionEstablished = true;
        promiseSettled = true;
        this.reconnectHandlers.forEach((handler) => {
          try {
            handler();
          } catch (error) {
            VibeLogger.logError(
              "unity_reconnect_handler_error",
              "Unity reconnect handler threw an error",
              {
                message: error instanceof Error ? error.message : String(error),
                stack: error instanceof Error ? error.stack : void 0
              }
            );
          }
        });
        resolve2();
      });
      currentSocket.on("error", (error) => {
        this._connected = false;
        if (!connectionEstablished) {
          finalizeInitialFailure(
            new Error(`Unity connection failed: ${error.message}`),
            "unity_connect_attempt_failed",
            "Unity socket error during connection attempt"
          );
          return;
        }
        VibeLogger.logError("unity_socket_error", "Unity socket error", {
          message: error.message
        });
        this.handleConnectionLoss();
      });
      currentSocket.on("close", () => {
        this._connected = false;
        if (!connectionEstablished) {
          finalizeInitialFailure(
            new Error("Unity connection closed before being established"),
            "unity_connect_closed_pre_handshake",
            "Unity socket closed during connection attempt"
          );
          return;
        }
        this.handleConnectionLoss();
      });
      currentSocket.on("end", () => {
        this._connected = false;
        if (!connectionEstablished) {
          finalizeInitialFailure(
            new Error("Unity connection ended before being established"),
            "unity_connect_end_pre_handshake",
            "Unity socket ended during connection attempt"
          );
          return;
        }
        this.handleConnectionLoss();
      });
      currentSocket.on("data", (data) => {
        this.messageHandler.handleIncomingData(data);
      });
    });
  }
  /**
   * Detect client name from stored value, environment variables, or default
   */
  detectClientName() {
    if (this.storedClientName) {
      return this.storedClientName;
    }
    return process.env.MCP_CLIENT_NAME || DEFAULT_CLIENT_NAME;
  }
  /**
   * Send client name to Unity for identification
   */
  async setClientName(clientName) {
    if (!this.connected) {
      return;
    }
    if (clientName) {
      this.storedClientName = clientName;
    }
    const finalClientName = clientName || this.detectClientName();
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: "set-client-name",
      params: {
        ClientName: finalClientName
      }
    };
    try {
      const response = await this.sendRequest(request);
      if (response.error) {
      }
    } catch (error) {
    }
  }
  /**
   * Send ping to Unity
   */
  async ping(message) {
    if (!this.connected) {
      throw new Error("Not connected to Unity");
    }
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: "ping",
      params: {
        Message: message
        // Updated to match PingSchema property name
      }
    };
    const response = await this.sendRequest(request, 1e3);
    if (response.error) {
      throw new Error(`Unity error: ${response.error.message}`);
    }
    return response.result || { Message: "Unity pong" };
  }
  /**
   * Get available tools from Unity
   */
  async getAvailableTools() {
    await this.ensureConnected();
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: "getAvailableTools",
      params: {}
    };
    const response = await this.sendRequest(request);
    if (response.error) {
      throw new Error(`Failed to get available tools: ${response.error.message}`);
    }
    return response.result || [];
  }
  /**
   * Get tool details from Unity
   */
  async getToolDetails(includeDevelopmentOnly = false) {
    await this.ensureConnected();
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: "get-tool-details",
      params: { IncludeDevelopmentOnly: includeDevelopmentOnly }
    };
    const response = await this.sendRequest(request);
    if (response.error) {
      throw new Error(`Failed to get tool details: ${response.error.message}`);
    }
    return response.result || [];
  }
  /**
   * Execute any Unity tool dynamically
   */
  async executeTool(toolName, params = {}) {
    if (!this.connected) {
      throw new Error(this.getOsSpecificReconnectMessage());
    }
    await this.setClientName();
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: toolName,
      params
    };
    const timeoutMs = TIMEOUTS.NETWORK;
    try {
      const response = await this.sendRequest(request, timeoutMs);
      return this.handleToolResponse(response, toolName);
    } catch (error) {
      if (error instanceof Error && error.message.includes("timed out")) {
      }
      throw error;
    }
  }
  /**
   * Build an OS-specific guidance message for temporary disconnection after compile.
   * Explicitly instructs how to wait before retrying without assuming a fixed duration.
   */
  getOsSpecificReconnectMessage() {
    const commonPrefix = "Not connected to Unity. If you just executed the compile tool, Unity reconnects automatically after compilation finishes. This can take from several seconds to tens of seconds depending on project size. Wait before your next tool call, then retry once.";
    const platform = typeof process !== "undefined" && typeof process.platform === "string" ? process.platform : "unknown";
    if (platform === "win32") {
      return `${commonPrefix} Examples: PowerShell: Start-Sleep -Seconds <seconds>; cmd: timeout /T <seconds> /NOBREAK. Avoid repeated retries; increase <seconds> if needed.`;
    }
    if (platform === "darwin" || platform === "linux") {
      return `${commonPrefix} Example: sleep <seconds>. Avoid repeated retries; increase <seconds> if needed.`;
    }
    return `${commonPrefix} Wait a bit longer if needed before retrying. Avoid repeated retries.`;
  }
  handleToolResponse(response, toolName) {
    if (response.error) {
      throw new Error(`Failed to execute tool '${toolName}': ${response.error.message}`);
    }
    return response.result;
  }
  /**
   * Generate unique request ID as string
   * Uses timestamp + process ID + random seed + counter for guaranteed uniqueness across processes
   */
  generateId() {
    if (this.requestIdCounter >= _UnityClient.MAX_COUNTER) {
      this.requestIdCounter = 1;
    } else {
      this.requestIdCounter++;
    }
    const timestamp = Date.now();
    const processId = this.processId;
    const randomSeed = this.randomSeed;
    const counter = this.requestIdCounter.toString().padStart(_UnityClient.COUNTER_PADDING, "0");
    return `ts_${timestamp}_${processId}_${randomSeed}_${counter}`;
  }
  /**
   * Send request and wait for response
   */
  async sendRequest(request, timeoutMs) {
    return new Promise((resolve2, reject) => {
      const timeout_duration = timeoutMs || TIMEOUTS.NETWORK;
      const timeoutTimer = safeSetTimeout(() => {
        this.messageHandler.clearPendingRequests(`Request ${ERROR_MESSAGES.TIMEOUT}`);
        reject(new Error(`Request ${ERROR_MESSAGES.TIMEOUT}`));
      }, timeout_duration);
      this.messageHandler.registerPendingRequest(
        request.id,
        (response) => {
          stopSafeTimer(timeoutTimer);
          resolve2(response);
        },
        (error) => {
          stopSafeTimer(timeoutTimer);
          reject(error);
        }
      );
      const requestStr = this.messageHandler.createRequest(
        request.method,
        request.params,
        request.id
      );
      if (this.socket) {
        this.socket.write(requestStr);
      }
    });
  }
  /**
   * Disconnect
   *
   * IMPORTANT: Always clean up timers when disconnecting!
   * Failure to properly clean up timers can cause orphaned processes
   * that prevent Node.js from exiting gracefully.
   */
  disconnect() {
    this.connectionManager.stopPolling();
    this.messageHandler.clearPendingRequests("Connection closed");
    this.messageHandler.clearBuffer();
    this.requestIdCounter = 0;
    if (this.socket) {
      this.socket.destroy();
      this.socket = null;
    }
    this._connected = false;
  }
  /**
   * Handle connection loss by delegating to UnityDiscovery
   */
  handleConnectionLoss() {
    this.connectionManager.triggerConnectionLost();
    if (this.unityDiscovery) {
      this.unityDiscovery.handleConnectionLost();
    }
  }
  /**
   * Set callback for when connection is restored
   */
  setReconnectedCallback(callback) {
    this.connectionManager.setReconnectedCallback(callback);
  }
  /**
   * Fetch tool details from Unity with development mode support
   */
  async fetchToolDetailsFromUnity(includeDevelopmentOnly = false) {
    const params = { IncludeDevelopmentOnly: includeDevelopmentOnly };
    const toolDetailsResponse = await this.executeTool("get-tool-details", params);
    const toolDetails = toolDetailsResponse?.Tools || toolDetailsResponse;
    if (!Array.isArray(toolDetails)) {
      return null;
    }
    return toolDetails;
  }
  /**
   * Check if Unity is available on specific port
   * Performs low-level TCP connection test with short timeout
   */
  static async isUnityAvailable(port) {
    return new Promise((resolve2) => {
      const socket = new net.Socket();
      const timeout = 500;
      const timer = setTimeout(() => {
        socket.destroy();
        resolve2(false);
      }, timeout);
      socket.connect(port, UNITY_CONNECTION.DEFAULT_HOST, () => {
        clearTimeout(timer);
        socket.destroy();
        resolve2(true);
      });
      socket.on("error", () => {
        clearTimeout(timer);
        resolve2(false);
      });
    });
  }
};

// src/unity-discovery.ts
var UnityDiscovery = class _UnityDiscovery {
  discoveryInterval = null;
  unityClient;
  onDiscoveredCallback = null;
  onConnectionLostCallback = null;
  isDiscovering = false;
  isDevelopment = false;
  discoveryAttemptCount = 0;
  // Singleton pattern to prevent multiple instances
  static instance = null;
  static activeTimerCount = 0;
  // Track active timers for debugging
  constructor(unityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === "development";
  }
  /**
   * Get singleton instance of UnityDiscovery
   */
  static getInstance(unityClient) {
    if (!_UnityDiscovery.instance) {
      _UnityDiscovery.instance = new _UnityDiscovery(unityClient);
    }
    return _UnityDiscovery.instance;
  }
  /**
   * Set callback for when Unity is discovered
   */
  setOnDiscoveredCallback(callback) {
    this.onDiscoveredCallback = callback;
  }
  /**
   * Set callback for when connection is lost
   */
  setOnConnectionLostCallback(callback) {
    this.onConnectionLostCallback = callback;
  }
  /**
   * Check if discovery is currently running
   */
  getIsDiscovering() {
    return this.isDiscovering;
  }
  /**
   * Get current polling interval based on attempt count
   */
  getCurrentPollingInterval() {
    const currentInterval = this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS ? POLLING.INITIAL_INTERVAL_MS : POLLING.EXTENDED_INTERVAL_MS;
    if (this.discoveryAttemptCount === POLLING.INITIAL_ATTEMPTS + 1) {
      VibeLogger.logInfo(
        "unity_discovery_polling_switch",
        "Switching from initial to extended polling interval",
        {
          previous_interval_ms: POLLING.INITIAL_INTERVAL_MS,
          new_interval_ms: POLLING.EXTENDED_INTERVAL_MS,
          discovery_attempt_count: this.discoveryAttemptCount,
          switch_threshold: POLLING.INITIAL_ATTEMPTS
        },
        void 0,
        "Polling interval changed to reduce CPU usage after initial attempts",
        "Monitor if this reduces system load while maintaining connection reliability"
      );
    }
    return currentInterval;
  }
  /**
   * Schedule next discovery attempt with adaptive interval
   */
  scheduleNextDiscovery() {
    const currentInterval = this.getCurrentPollingInterval();
    const isInitialPolling = this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS;
    VibeLogger.logDebug(
      "unity_discovery_scheduling_next",
      "Scheduling next discovery attempt",
      {
        next_interval_ms: currentInterval,
        discovery_attempt_count: this.discoveryAttemptCount,
        is_initial_polling: isInitialPolling,
        polling_type: isInitialPolling ? "initial_fast" : "extended_slow"
      },
      void 0,
      `Next discovery scheduled in ${currentInterval}ms`
    );
    this.discoveryInterval = setTimeout(() => {
      void this.unifiedDiscoveryAndConnectionCheck();
      if (this.discoveryInterval) {
        this.scheduleNextDiscovery();
      }
    }, currentInterval);
  }
  /**
   * Start Unity discovery polling with unified connection management
   */
  start() {
    if (this.discoveryInterval) {
      return;
    }
    if (this.isDiscovering) {
      return;
    }
    this.discoveryAttemptCount = 0;
    void this.unifiedDiscoveryAndConnectionCheck();
    this.scheduleNextDiscovery();
    _UnityDiscovery.activeTimerCount++;
  }
  /**
   * Stop Unity discovery polling
   */
  stop() {
    if (this.discoveryInterval) {
      clearTimeout(this.discoveryInterval);
      this.discoveryInterval = null;
    }
    this.isDiscovering = false;
    this.discoveryAttemptCount = 0;
    _UnityDiscovery.activeTimerCount = Math.max(0, _UnityDiscovery.activeTimerCount - 1);
  }
  /**
   * Force reset discovery state (for debugging and recovery)
   */
  forceResetDiscoveryState() {
    this.isDiscovering = false;
    VibeLogger.logWarning(
      "unity_discovery_state_force_reset",
      "Discovery state forcibly reset",
      { was_discovering: true },
      void 0,
      "Manual recovery from stuck discovery state"
    );
  }
  /**
   * Unified discovery and connection checking
   * Handles both Unity discovery and connection health monitoring
   */
  async unifiedDiscoveryAndConnectionCheck() {
    const correlationId = VibeLogger.generateCorrelationId();
    if (this.isDiscovering) {
      VibeLogger.logDebug(
        "unity_discovery_skip_in_progress",
        "Discovery already in progress - skipping",
        { is_discovering: true, active_timer_count: _UnityDiscovery.activeTimerCount },
        correlationId,
        "Another discovery cycle is already running - this is normal behavior."
      );
      return;
    }
    this.logDiscoveryCycleStart(correlationId);
    try {
      const shouldContinueDiscovery = await this.handleConnectionHealthCheck(correlationId);
      if (!shouldContinueDiscovery) {
        return;
      }
      await this.executeUnityDiscovery(correlationId);
    } catch (error) {
      VibeLogger.logError(
        "unity_discovery_cycle_error",
        "Discovery cycle encountered error",
        {
          error_message: error instanceof Error ? error.message : String(error),
          is_discovering: this.isDiscovering,
          correlation_id: correlationId
        },
        correlationId,
        "Discovery cycle failed - forcing state reset to prevent hang"
      );
    } finally {
      this.finalizeCycleWithCleanup(correlationId);
    }
  }
  /**
   * Log discovery cycle initialization
   */
  logDiscoveryCycleStart(correlationId) {
    this.isDiscovering = true;
    this.discoveryAttemptCount++;
    const currentInterval = this.getCurrentPollingInterval();
    VibeLogger.logDebug(
      "unity_discovery_state_set",
      "Discovery state set to true",
      { is_discovering: true, correlation_id: correlationId },
      correlationId,
      "Discovery state flag set - starting cycle"
    );
    VibeLogger.logInfo(
      "unity_discovery_cycle_start",
      "Starting unified discovery and connection check cycle",
      {
        unity_connected: this.unityClient.connected,
        polling_interval_ms: currentInterval,
        discovery_attempt_count: this.discoveryAttemptCount,
        is_initial_polling: this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS,
        active_timer_count: _UnityDiscovery.activeTimerCount
      },
      correlationId,
      "This cycle checks connection health and attempts Unity discovery if needed."
    );
  }
  /**
   * Handle connection health check and determine if discovery should continue
   */
  async handleConnectionHealthCheck(correlationId) {
    if (this.unityClient.connected) {
      const isConnectionHealthy = await this.checkConnectionHealth();
      if (isConnectionHealthy) {
        VibeLogger.logInfo(
          "unity_discovery_connection_healthy",
          "Connection is healthy - stopping discovery",
          { connection_healthy: true },
          correlationId
        );
        this.stop();
        return false;
      } else {
        VibeLogger.logWarning(
          "unity_discovery_connection_unhealthy",
          "Connection appears unhealthy - continuing discovery without assuming loss",
          { connection_healthy: false },
          correlationId,
          "Connection health check failed. Will continue discovery but not assume complete loss.",
          "Connection may recover on next cycle. Monitor for persistent issues."
        );
      }
    }
    return true;
  }
  /**
   * Execute Unity discovery with timeout protection
   */
  async executeUnityDiscovery(_correlationId) {
    await Promise.race([
      this.discoverUnityOnPorts(),
      new Promise(
        (_, reject) => setTimeout(() => reject(new Error("Unity discovery timeout - 5 seconds")), 5e3)
      )
    ]);
  }
  /**
   * Finalize discovery cycle with cleanup and logging
   */
  finalizeCycleWithCleanup(correlationId) {
    VibeLogger.logDebug(
      "unity_discovery_cycle_end",
      "Discovery cycle completed - resetting state",
      {
        is_discovering_before: this.isDiscovering,
        is_discovering_after: false,
        correlation_id: correlationId
      },
      correlationId,
      "Discovery cycle finished and state reset to prevent hang",
      void 0,
      true
    );
    this.isDiscovering = false;
  }
  /**
   * Check if the current connection is healthy with timeout protection
   */
  async checkConnectionHealth() {
    try {
      const healthCheck = await Promise.race([
        this.unityClient.testConnection(),
        new Promise(
          (_, reject) => setTimeout(() => reject(new Error("Connection health check timeout")), 1e3)
        )
      ]);
      return healthCheck;
    } catch (error) {
      return false;
    }
  }
  /**
   * Discover Unity by checking specified port
   */
  async discoverUnityOnPorts() {
    const correlationId = VibeLogger.generateCorrelationId();
    const unityTcpPort = process.env.UNITY_TCP_PORT;
    if (!unityTcpPort) {
      throw new Error("UNITY_TCP_PORT environment variable is required but not set");
    }
    const port = parseInt(unityTcpPort, 10);
    if (isNaN(port) || port <= 0 || port > 65535) {
      throw new Error(`UNITY_TCP_PORT must be a valid port number (1-65535), got: ${unityTcpPort}`);
    }
    VibeLogger.logInfo(
      "unity_discovery_port_scan_start",
      "Starting Unity port discovery scan",
      {
        target_port: port
      },
      correlationId,
      "Checking specified port for Unity MCP server."
    );
    try {
      if (await UnityClient.isUnityAvailable(port)) {
        VibeLogger.logInfo(
          "unity_discovery_success",
          "Unity discovered and connection established",
          {
            discovered_port: port
          },
          correlationId,
          "Unity MCP server found and connection established successfully.",
          "Monitor for tools/list_changed notifications after this discovery."
        );
        this.unityClient.updatePort(port);
        if (this.onDiscoveredCallback) {
          await this.onDiscoveredCallback(port);
        }
        return;
      }
    } catch (error) {
      VibeLogger.logDebug(
        "unity_discovery_port_check_failed",
        "Unity availability check failed for specified port",
        {
          target_port: port,
          error_message: error instanceof Error ? error.message : String(error),
          error_type: error instanceof Error ? error.constructor.name : typeof error
        },
        correlationId,
        "Expected failure when Unity is not running on this port. Will continue polling.",
        "This is normal during Unity startup or when Unity is not running."
      );
    }
    VibeLogger.logWarning(
      "unity_discovery_no_unity_found",
      "No Unity server found on specified port",
      {
        target_port: port
      },
      correlationId,
      "Unity MCP server not found on the specified port. Unity may not be running or using a different port.",
      "Check Unity console for MCP server status and verify port configuration."
    );
  }
  /**
   * Force immediate Unity discovery for connection recovery
   */
  async forceDiscovery() {
    if (this.unityClient.connected) {
      return true;
    }
    await this.unifiedDiscoveryAndConnectionCheck();
    return this.unityClient.connected;
  }
  /**
   * Handle connection lost event (called by UnityClient)
   */
  handleConnectionLost() {
    const correlationId = VibeLogger.generateCorrelationId();
    VibeLogger.logInfo(
      "unity_discovery_connection_lost_handler",
      "Handling connection lost event",
      {
        was_discovering: this.isDiscovering,
        has_discovery_interval: this.discoveryInterval !== null,
        active_timer_count: _UnityDiscovery.activeTimerCount
      },
      correlationId,
      "Connection lost event received - preparing for recovery"
    );
    this.isDiscovering = false;
    this.discoveryAttemptCount = 0;
    setTimeout(() => {
      VibeLogger.logInfo(
        "unity_discovery_restart_after_connection_lost",
        "Restarting discovery after connection lost delay",
        {
          has_discovery_interval: this.discoveryInterval !== null
        },
        correlationId,
        "Starting discovery with delay to allow Unity server restart"
      );
      if (!this.discoveryInterval) {
        this.start();
      }
    }, 2e3);
    if (this.onConnectionLostCallback) {
      this.onConnectionLostCallback();
    }
  }
  /**
   * Get debugging information about current timer state
   */
  getDebugInfo() {
    return {
      isTimerActive: this.discoveryInterval !== null,
      isDiscovering: this.isDiscovering,
      activeTimerCount: _UnityDiscovery.activeTimerCount,
      isConnected: this.unityClient.connected,
      intervalMs: this.getCurrentPollingInterval(),
      discoveryAttemptCount: this.discoveryAttemptCount,
      isInitialPolling: this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS,
      hasSingleton: _UnityDiscovery.instance !== null
    };
  }
};

// src/unity-connection-manager.ts
var UnityConnectionManager = class {
  unityClient;
  unityDiscovery;
  isDevelopment;
  isInitialized = false;
  isReconnecting = false;
  constructor(unityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
    this.unityDiscovery = UnityDiscovery.getInstance(this.unityClient);
    this.unityClient.setUnityDiscovery(this.unityDiscovery);
  }
  /**
   * Get Unity discovery instance
   */
  getUnityDiscovery() {
    return this.unityDiscovery;
  }
  /**
   * Wait for Unity connection with timeout
   */
  async waitForUnityConnectionWithTimeout(timeoutMs) {
    return new Promise((resolve2, reject) => {
      const timeout = setTimeout(() => {
        reject(new Error(`Unity connection timeout after ${timeoutMs}ms`));
      }, timeoutMs);
      const checkConnection = () => {
        if (this.unityClient.connected) {
          clearTimeout(timeout);
          resolve2();
          return;
        }
        if (this.isInitialized) {
          const connectionInterval = setInterval(() => {
            if (this.unityClient.connected) {
              clearTimeout(timeout);
              clearInterval(connectionInterval);
              resolve2();
            }
          }, 100);
          return;
        }
        this.initialize(() => {
          return new Promise((resolveCallback) => {
            clearTimeout(timeout);
            resolve2();
            resolveCallback();
          });
        });
      };
      void checkConnection();
    });
  }
  /**
   * Handle Unity discovery and establish connection
   */
  async handleUnityDiscovered(onConnectionEstablished) {
    try {
      await this.unityClient.ensureConnected();
      if (onConnectionEstablished) {
        await onConnectionEstablished();
      }
      this.unityDiscovery.stop();
    } catch (error) {
    }
  }
  /**
   * Initialize connection manager
   */
  initialize(onConnectionEstablished) {
    if (this.isInitialized) {
      return;
    }
    this.isInitialized = true;
    this.unityDiscovery.setOnDiscoveredCallback(async (_port) => {
      await this.handleUnityDiscovered(onConnectionEstablished);
    });
    this.unityDiscovery.start();
  }
  /**
   * Setup reconnection callback
   */
  setupReconnectionCallback(callback) {
    this.unityClient.setReconnectedCallback(() => {
      if (this.isReconnecting) {
        return;
      }
      this.isReconnecting = true;
      void this.unityDiscovery.forceDiscovery().then(() => {
        return callback();
      }).finally(() => {
        this.isReconnecting = false;
      });
    });
  }
  /**
   * Check if Unity is connected
   */
  isConnected() {
    return this.unityClient.connected;
  }
  /**
   * Ensure Unity connection is established
   */
  async ensureConnected(timeoutMs = 1e4) {
    if (this.isConnected()) {
      return;
    }
    await this.waitForUnityConnectionWithTimeout(timeoutMs);
  }
  /**
   * Test connection (validate connection state)
   */
  async testConnection() {
    try {
      return await this.unityClient.testConnection();
    } catch {
      return false;
    }
  }
  /**
   * Disconnect from Unity
   */
  disconnect() {
    this.unityDiscovery.stop();
    this.unityClient.disconnect();
  }
};

// src/tools/base-tool.ts
var BaseTool = class {
  context;
  constructor(context) {
    this.context = context;
  }
  /**
   * Main method for tool execution
   */
  async handle(args) {
    try {
      const validatedArgs = this.validateArgs(args);
      const result = await this.execute(validatedArgs);
      return this.formatResponse(result);
    } catch (error) {
      return this.formatErrorResponse(error);
    }
  }
  /**
   * Format success response (can be overridden in subclass)
   */
  formatResponse(result) {
    if (typeof result === "object" && result !== null && "content" in result) {
      return result;
    }
    return {
      content: [
        {
          type: "text",
          text: typeof result === "string" ? result : JSON.stringify(result, null, 2)
        }
      ]
    };
  }
  /**
   * Format error response
   */
  formatErrorResponse(error) {
    const errorMessage = error instanceof Error ? error.message : "Unknown error";
    return {
      content: [
        {
          type: "text",
          text: `Error in ${this.name}: ${errorMessage}`
        }
      ]
    };
  }
};

// src/tools/dynamic-unity-command-tool.ts
var DynamicUnityCommandTool = class extends BaseTool {
  name;
  description;
  inputSchema;
  toolName;
  constructor(context, toolName, description, parameterSchema) {
    super(context);
    this.toolName = toolName;
    this.name = toolName;
    this.description = description;
    this.inputSchema = this.generateInputSchema(parameterSchema);
  }
  generateInputSchema(parameterSchema) {
    if (this.hasNoParameters(parameterSchema)) {
      return {
        type: "object",
        properties: {},
        additionalProperties: false
      };
    }
    const properties = {};
    const required = [];
    if (!parameterSchema) {
      throw new Error("Parameter schema is undefined");
    }
    const propertiesObj = parameterSchema[PARAMETER_SCHEMA.PROPERTIES_PROPERTY];
    for (const [propName, propInfo] of Object.entries(propertiesObj)) {
      const info = propInfo;
      const property = {
        type: this.convertType(String(info[PARAMETER_SCHEMA.TYPE_PROPERTY] || "string")),
        description: String(
          info[PARAMETER_SCHEMA.DESCRIPTION_PROPERTY] || `Parameter: ${propName}`
        )
      };
      const defaultValue = info[PARAMETER_SCHEMA.DEFAULT_VALUE_PROPERTY];
      if (defaultValue !== void 0 && defaultValue !== null) {
        property.default = defaultValue;
      }
      const enumValues = info[PARAMETER_SCHEMA.ENUM_PROPERTY];
      if (enumValues && Array.isArray(enumValues) && enumValues.length > 0) {
        property.enum = enumValues;
      }
      if (info[PARAMETER_SCHEMA.TYPE_PROPERTY] === "array" && defaultValue && Array.isArray(defaultValue)) {
        property.items = {
          type: "string"
        };
        property.default = defaultValue;
      }
      Object.defineProperty(properties, propName, {
        value: property,
        writable: true,
        enumerable: true,
        configurable: true
      });
    }
    if (!parameterSchema) {
      throw new Error("Parameter schema is undefined");
    }
    const requiredParams = parameterSchema[PARAMETER_SCHEMA.REQUIRED_PROPERTY];
    if (requiredParams && Array.isArray(requiredParams)) {
      required.push(...requiredParams);
    }
    const schema = {
      type: "object",
      properties,
      required: required.length > 0 ? required : void 0
    };
    return schema;
  }
  convertType(unityType) {
    switch (unityType?.toLowerCase()) {
      case "string":
        return "string";
      case "number":
      case "int":
      case "float":
      case "double":
        return "number";
      case "boolean":
      case "bool":
        return "boolean";
      case "array":
        return "array";
      default:
        return "string";
    }
  }
  hasNoParameters(parameterSchema) {
    if (!parameterSchema) {
      return true;
    }
    const properties = parameterSchema[PARAMETER_SCHEMA.PROPERTIES_PROPERTY];
    if (!properties || typeof properties !== "object") {
      return true;
    }
    return Object.keys(properties).length === 0;
  }
  validateArgs(args) {
    if (!this.inputSchema.properties || Object.keys(this.inputSchema.properties).length === 0) {
      return {};
    }
    return args || {};
  }
  async execute(args) {
    try {
      const actualArgs = this.validateArgs(args);
      const result = await this.context.unityClient.executeTool(this.toolName, actualArgs);
      return {
        content: [
          {
            type: "text",
            text: typeof result === "string" ? result : JSON.stringify(result, null, 2)
          }
        ]
      };
    } catch (error) {
      return this.formatErrorResponse(error);
    }
  }
  formatErrorResponse(error) {
    const errorMessage = error instanceof Error ? error.message : "Unknown error";
    return {
      content: [
        {
          type: "text",
          text: `Failed to execute tool '${this.toolName}': ${errorMessage}`
        }
      ]
    };
  }
};

// src/domain/errors.ts
var DomainError = class extends Error {
  constructor(message, details) {
    super(message);
    this.details = details;
    this.name = this.constructor.name;
    Object.setPrototypeOf(this, new.target.prototype);
  }
};
var ConnectionError = class extends DomainError {
  code = "CONNECTION_ERROR";
};
var ToolExecutionError = class extends DomainError {
  code = "TOOL_EXECUTION_ERROR";
};
var ValidationError = class extends DomainError {
  code = "VALIDATION_ERROR";
};
var DiscoveryError = class extends DomainError {
  code = "DISCOVERY_ERROR";
};
var ClientCompatibilityError = class extends DomainError {
  code = "CLIENT_COMPATIBILITY_ERROR";
};

// src/infrastructure/errors.ts
var InfrastructureError = class extends Error {
  constructor(message, technicalDetails, originalError) {
    super(message);
    this.technicalDetails = technicalDetails;
    this.originalError = originalError;
    this.name = this.constructor.name;
    Object.setPrototypeOf(this, new.target.prototype);
  }
  /**
   * 技術的詳細を含む完全なエラー情報を取得
   */
  getFullErrorInfo() {
    return {
      message: this.message,
      category: this.category,
      technicalDetails: this.technicalDetails,
      originalError: this.originalError?.message,
      stack: this.stack
    };
  }
};
var UnityCommunicationError = class extends InfrastructureError {
  constructor(message, unityEndpoint, requestData, originalError) {
    super(message, { unityEndpoint, requestData }, originalError);
    this.unityEndpoint = unityEndpoint;
    this.requestData = requestData;
  }
  category = "UNITY_COMMUNICATION";
};
var ToolManagementError = class extends InfrastructureError {
  constructor(message, toolName, toolData, originalError) {
    super(message, { toolName, toolData }, originalError);
    this.toolName = toolName;
    this.toolData = toolData;
  }
  category = "TOOL_MANAGEMENT";
};

// src/application/error-converter.ts
var ErrorConverter = class {
  /**
   * Infrastructure層のエラーをDomain層のエラーに変換
   *
   * @param error 変換対象のエラー
   * @param operation 操作名（ログ用）
   * @param correlationId 相関ID
   * @returns 変換されたDomainError
   */
  static convertToDomainError(error, operation, correlationId) {
    if (error instanceof DomainError) {
      return error;
    }
    if (error instanceof InfrastructureError) {
      return this.convertInfrastructureError(error, operation, correlationId);
    }
    if (error instanceof Error) {
      return this.convertGenericError(error, operation, correlationId);
    }
    return this.convertUnknownError(error, operation, correlationId);
  }
  /**
   * Infrastructure層エラーをDomain層エラーに変換
   */
  static convertInfrastructureError(error, operation, correlationId) {
    VibeLogger.logError(
      `${operation}_infrastructure_error`,
      `Infrastructure error during ${operation}`,
      error.getFullErrorInfo(),
      correlationId,
      "Error Converter logging technical details before domain conversion"
    );
    switch (error.category) {
      case "UNITY_COMMUNICATION":
        return new ConnectionError(`Unity communication failed: ${error.message}`, {
          original_category: error.category,
          unity_endpoint: error.unityEndpoint
        });
      case "TOOL_MANAGEMENT":
        return new ToolExecutionError(`Tool management failed: ${error.message}`, {
          original_category: error.category,
          tool_name: error.toolName
        });
      case "SERVICE_RESOLUTION":
        return new ValidationError(`Service resolution failed: ${error.message}`, {
          original_category: error.category,
          service_token: error.serviceToken
        });
      case "NETWORK":
        return new DiscoveryError(`Network operation failed: ${error.message}`, {
          original_category: error.category,
          endpoint: error.endpoint,
          port: error.port
        });
      case "MCP_PROTOCOL":
        return new ClientCompatibilityError(`MCP protocol error: ${error.message}`, {
          original_category: error.category,
          protocol_version: error.protocolVersion
        });
      default:
        return new ToolExecutionError(`Infrastructure error: ${error.message}`, {
          original_category: error.category
        });
    }
  }
  /**
   * 一般的なErrorをDomain層エラーに変換
   */
  static convertGenericError(error, operation, correlationId) {
    VibeLogger.logError(
      `${operation}_generic_error`,
      `Generic error during ${operation}`,
      {
        error_name: error.name,
        error_message: error.message,
        stack: error.stack
      },
      correlationId,
      "Error Converter handling generic Error instance"
    );
    const message = error.message.toLowerCase();
    if (message.includes("connection") || message.includes("connect")) {
      return new ConnectionError(`Connection error: ${error.message}`);
    }
    if (message.includes("tool") || message.includes("execute")) {
      return new ToolExecutionError(`Tool execution error: ${error.message}`);
    }
    if (message.includes("validation") || message.includes("invalid")) {
      return new ValidationError(`Validation error: ${error.message}`);
    }
    if (message.includes("discovery") || message.includes("network")) {
      return new DiscoveryError(`Discovery error: ${error.message}`);
    }
    return new ToolExecutionError(`Unexpected error: ${error.message}`);
  }
  /**
   * 不明なエラーオブジェクトをDomain層エラーに変換
   */
  static convertUnknownError(error, operation, correlationId) {
    const errorString = typeof error === "string" ? error : JSON.stringify(error);
    VibeLogger.logError(
      `${operation}_unknown_error`,
      `Unknown error type during ${operation}`,
      { error_value: error, error_type: typeof error },
      correlationId,
      "Error Converter handling unknown error type"
    );
    return new ToolExecutionError(`Unknown error occurred: ${errorString}`);
  }
  /**
   * エラーが回復可能かどうかを判定
   *
   * @param error 判定対象のエラー
   * @returns 回復可能な場合true
   */
  static isRecoverable(error) {
    switch (error.code) {
      case "CONNECTION_ERROR":
      case "DISCOVERY_ERROR":
        return true;
      // 接続・発見エラーは再試行可能
      case "VALIDATION_ERROR":
      case "CLIENT_COMPATIBILITY_ERROR":
        return false;
      // 検証・互換性エラーは回復不可能
      case "TOOL_EXECUTION_ERROR":
        return true;
      // ツール実行エラーは状況によって再試行可能
      default:
        return false;
    }
  }
};

// src/domain/use-cases/refresh-tools-use-case.ts
var RefreshToolsUseCase = class {
  connectionService;
  toolManagementService;
  toolQueryService;
  constructor(connectionService, toolManagementService, toolQueryService) {
    this.connectionService = connectionService;
    this.toolManagementService = toolManagementService;
    this.toolQueryService = toolQueryService;
  }
  /**
   * Execute the tool refresh workflow
   *
   * @param request Tool refresh request
   * @returns Tool refresh response
   */
  async execute(request) {
    const correlationId = VibeLogger.generateCorrelationId();
    VibeLogger.logInfo(
      "refresh_tools_use_case_start",
      "Starting tool refresh workflow",
      { include_development: request.includeDevelopmentOnly },
      correlationId,
      "UseCase orchestrating tool refresh workflow for domain reload recovery"
    );
    try {
      await this.ensureUnityConnection(correlationId);
      await this.refreshToolsFromUnity(correlationId);
      const refreshedTools = this.toolQueryService.getAllTools();
      const response = {
        tools: refreshedTools,
        refreshedAt: (/* @__PURE__ */ new Date()).toISOString()
      };
      VibeLogger.logInfo(
        "refresh_tools_use_case_success",
        "Tool refresh workflow completed successfully",
        {
          tool_count: refreshedTools.length,
          refreshed_at: response.refreshedAt
        },
        correlationId,
        "Tool refresh completed successfully - Unity tools updated after domain reload"
      );
      return response;
    } catch (error) {
      return this.handleRefreshError(error, request, correlationId);
    }
  }
  /**
   * Ensure Unity connection is established
   *
   * @param correlationId Correlation ID for logging
   * @throws ConnectionError if connection cannot be established
   */
  async ensureUnityConnection(correlationId) {
    if (!this.connectionService.isConnected()) {
      VibeLogger.logWarning(
        "refresh_tools_unity_not_connected",
        "Unity not connected during tool refresh, attempting to establish connection",
        { connected: false },
        correlationId,
        "Unity connection required for tool refresh after domain reload"
      );
      try {
        await this.connectionService.ensureConnected(1e4);
      } catch (error) {
        const domainError = ErrorConverter.convertToDomainError(
          error,
          "refresh_tools_connection_ensure",
          correlationId
        );
        throw domainError;
      }
    }
    VibeLogger.logDebug(
      "refresh_tools_connection_verified",
      "Unity connection verified for tool refresh",
      { connected: true },
      correlationId,
      "Connection ready for tool refresh after domain reload"
    );
  }
  /**
   * Refresh tools from Unity by re-initializing dynamic tools
   *
   * @param correlationId Correlation ID for logging
   */
  async refreshToolsFromUnity(correlationId) {
    try {
      VibeLogger.logDebug(
        "refresh_tools_initializing",
        "Re-initializing dynamic tools from Unity",
        {},
        correlationId,
        "Fetching latest tool definitions from Unity after domain reload"
      );
      await this.toolManagementService.initializeTools();
      VibeLogger.logInfo(
        "refresh_tools_initialized",
        "Dynamic tools re-initialized successfully from Unity",
        { tool_count: this.toolQueryService.getToolsCount() },
        correlationId,
        "Tool definitions updated from Unity after domain reload"
      );
    } catch (error) {
      const domainError = ErrorConverter.convertToDomainError(
        error,
        "refresh_tools_initialization",
        correlationId
      );
      throw domainError;
    }
  }
  /**
   * Handle tool refresh errors
   *
   * @param error Error that occurred
   * @param request Original request
   * @param correlationId Correlation ID for logging
   * @returns RefreshToolsResponse with error state
   */
  handleRefreshError(error, request, correlationId) {
    const errorMessage = error instanceof Error ? error.message : "Unknown error";
    VibeLogger.logError(
      "refresh_tools_use_case_error",
      "Tool refresh workflow failed",
      {
        include_development: request.includeDevelopmentOnly,
        error_message: errorMessage,
        error_type: error instanceof Error ? error.constructor.name : typeof error
      },
      correlationId,
      "UseCase workflow failed - returning empty tools list to prevent client errors"
    );
    return {
      tools: [],
      refreshedAt: (/* @__PURE__ */ new Date()).toISOString()
    };
  }
};

// src/unity-tool-manager.ts
var UnityToolManager = class {
  unityClient;
  isDevelopment;
  dynamicTools = /* @__PURE__ */ new Map();
  isRefreshing = false;
  clientName = "";
  connectionManager;
  // Will be injected for UseCase
  constructor(unityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
  }
  /**
   * Set connection manager for UseCase integration (Phase 3.2)
   */
  setConnectionManager(connectionManager) {
    this.connectionManager = connectionManager;
  }
  /**
   * Set client name for Unity communication
   */
  setClientName(clientName) {
    this.clientName = clientName;
  }
  /**
   * Get dynamic tools map
   */
  getDynamicTools() {
    return this.dynamicTools;
  }
  /**
   * Get tools from Unity
   */
  async getToolsFromUnity() {
    if (!this.unityClient.connected) {
      return [];
    }
    try {
      const toolDetails = await this.unityClient.fetchToolDetailsFromUnity(this.isDevelopment);
      if (!toolDetails) {
        throw new UnityCommunicationError(
          "Unity returned no tool details",
          "Unity Editor tools endpoint",
          { development_mode: this.isDevelopment }
        );
      }
      this.createDynamicToolsFromTools(toolDetails);
      const tools = [];
      for (const [toolName, dynamicTool] of this.dynamicTools) {
        tools.push({
          name: toolName,
          description: dynamicTool.description,
          inputSchema: this.convertToMcpSchema(dynamicTool.inputSchema)
        });
      }
      return tools;
    } catch (error) {
      if (error instanceof UnityCommunicationError) {
        throw error;
      }
      throw new UnityCommunicationError(
        "Failed to retrieve tools from Unity",
        "Unity Editor tools endpoint",
        { development_mode: this.isDevelopment },
        error instanceof Error ? error : void 0
      );
    }
  }
  /**
   * Initialize dynamic Unity tools
   */
  async initializeDynamicTools() {
    try {
      await this.unityClient.ensureConnected();
      const toolDetails = await this.unityClient.fetchToolDetailsFromUnity(this.isDevelopment);
      if (!toolDetails) {
        throw new UnityCommunicationError(
          "Unity returned no tool details during initialization",
          "Unity Editor tools endpoint",
          { development_mode: this.isDevelopment }
        );
      }
      this.createDynamicToolsFromTools(toolDetails);
    } catch (error) {
      if (error instanceof UnityCommunicationError) {
        throw error;
      }
      throw new ToolManagementError(
        "Failed to initialize dynamic tools",
        void 0,
        { development_mode: this.isDevelopment },
        error instanceof Error ? error : void 0
      );
    }
  }
  /**
   * Create dynamic tools from Unity tool details
   */
  createDynamicToolsFromTools(toolDetails) {
    this.dynamicTools.clear();
    const toolContext = { unityClient: this.unityClient };
    for (const toolInfo of toolDetails) {
      const toolName = toolInfo.name;
      const description = toolInfo.description || `Execute Unity tool: ${toolName}`;
      const parameterSchema = toolInfo.parameterSchema;
      const displayDevelopmentOnly = toolInfo.displayDevelopmentOnly || false;
      if (displayDevelopmentOnly && !this.isDevelopment) {
        continue;
      }
      const finalToolName = toolName;
      const dynamicTool = new DynamicUnityCommandTool(
        toolContext,
        toolName,
        description,
        parameterSchema
        // Type assertion for schema compatibility
      );
      this.dynamicTools.set(finalToolName, dynamicTool);
    }
  }
  /**
   * Refresh dynamic tools by re-fetching from Unity
   * This method can be called to update the tool list when Unity tools change
   *
   * IMPORTANT: This method is critical for domain reload recovery
   */
  async refreshDynamicTools(sendNotification) {
    try {
      if (this.connectionManager && this.connectionManager.isConnected()) {
        await this.refreshToolsWithUseCase(sendNotification);
        return;
      }
      await this.refreshToolsFallback(sendNotification);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      VibeLogger.logError(
        "unity_tool_manager_refresh_failed",
        "Failed to refresh dynamic tools",
        { error: errorMessage },
        void 0,
        "Tool refresh failed - domain reload recovery may be impacted"
      );
      await this.executeUltimateToolsFallback(sendNotification);
    }
  }
  /**
   * Refresh tools using RefreshToolsUseCase (preferred method)
   */
  async refreshToolsWithUseCase(sendNotification) {
    if (!this.connectionManager) {
      throw new Error("ConnectionManager is required for UseCase-based refresh");
    }
    const refreshToolsUseCase = new RefreshToolsUseCase(this.connectionManager, this, this);
    const result = await refreshToolsUseCase.execute({
      includeDevelopmentOnly: this.isDevelopment
    });
    VibeLogger.logInfo(
      "unity_tool_manager_refresh_completed",
      "Dynamic tools refreshed successfully via UseCase",
      { tool_count: result.tools.length, refreshed_at: result.refreshedAt },
      void 0,
      "Tool refresh completed - ready for domain reload recovery"
    );
    if (sendNotification) {
      sendNotification();
    }
  }
  /**
   * Refresh tools using direct initialization (fallback method)
   */
  async refreshToolsFallback(sendNotification) {
    VibeLogger.logDebug(
      "unity_tool_manager_fallback_refresh",
      "Using fallback refresh method (connectionManager not available)",
      {},
      void 0,
      "Direct initialization fallback for domain reload recovery"
    );
    await this.initializeDynamicTools();
    if (sendNotification) {
      sendNotification();
    }
  }
  /**
   * Execute ultimate fallback for tool refresh (last resort)
   */
  async executeUltimateToolsFallback(sendNotification) {
    try {
      await this.initializeDynamicTools();
      if (sendNotification) {
        sendNotification();
      }
    } catch (fallbackError) {
      VibeLogger.logError(
        "unity_tool_manager_fallback_failed",
        "Fallback tool initialization also failed",
        { error: fallbackError instanceof Error ? fallbackError.message : String(fallbackError) },
        void 0,
        "Both UseCase and fallback failed - domain reload recovery compromised"
      );
    }
  }
  /**
   * Safe version of refreshDynamicTools that prevents duplicate execution
   */
  async refreshDynamicToolsSafe(sendNotification) {
    if (this.isRefreshing) {
      return;
    }
    this.isRefreshing = true;
    try {
      await this.refreshDynamicTools(sendNotification);
    } finally {
      this.isRefreshing = false;
    }
  }
  /**
   * Check if tool exists
   */
  hasTool(toolName) {
    return this.dynamicTools.has(toolName);
  }
  /**
   * Get tool by name
   */
  getTool(toolName) {
    return this.dynamicTools.get(toolName);
  }
  /**
   * Get all tools as array
   */
  getAllTools() {
    const tools = [];
    for (const [toolName, dynamicTool] of this.dynamicTools) {
      tools.push({
        name: toolName,
        description: dynamicTool.description,
        inputSchema: this.convertToMcpSchema(dynamicTool.inputSchema)
      });
    }
    return tools;
  }
  /**
   * Get tools count
   */
  getToolsCount() {
    return this.dynamicTools.size;
  }
  // IToolService interface compatibility methods
  /**
   * Initialize dynamic tools (IToolService interface)
   */
  async initializeTools() {
    return this.initializeDynamicTools();
  }
  /**
   * Refresh dynamic tools (IToolService interface)
   */
  async refreshTools() {
    return this.initializeDynamicTools();
  }
  /**
   * Convert input schema to MCP-compatible format safely
   */
  convertToMcpSchema(inputSchema) {
    if (!inputSchema || typeof inputSchema !== "object") {
      return { type: "object" };
    }
    const schema = inputSchema;
    const result = { type: "object" };
    if (schema.properties && typeof schema.properties === "object") {
      result.properties = schema.properties;
    }
    if (Array.isArray(schema.required)) {
      result.required = schema.required;
    }
    return result;
  }
};

// src/mcp-client-compatibility.ts
var McpClientCompatibility = class {
  unityClient;
  clientName = DEFAULT_CLIENT_NAME;
  constructor(unityClient) {
    this.unityClient = unityClient;
  }
  /**
   * Set client name
   */
  setClientName(clientName) {
    this.clientName = clientName;
  }
  /**
   * Setup client compatibility configuration with logging
   */
  setupClientCompatibility(clientName) {
    this.setClientName(clientName);
    this.logClientCompatibility(clientName);
  }
  /**
   * Get client name
   */
  getClientName() {
    return this.clientName;
  }
  /**
   * Check if client doesn't support list_changed notifications
   */
  isListChangedUnsupported(clientName) {
    return !this.isListChangedSupported(clientName);
  }
  /**
   * Handle client name initialization and setup
   */
  async handleClientNameInitialization() {
    if (!this.clientName) {
      const fallbackName = process.env.MCP_CLIENT_NAME;
      if (fallbackName) {
        this.clientName = fallbackName;
        await this.unityClient.setClientName(fallbackName);
      } else {
      }
    } else {
      await this.unityClient.setClientName(this.clientName);
    }
    this.unityClient.onReconnect(() => {
      void this.unityClient.setClientName(this.clientName);
    });
  }
  /**
   * Initialize client with name
   */
  async initializeClient(clientName) {
    this.setClientName(clientName);
    await this.handleClientNameInitialization();
  }
  /**
   * Check if client supports list_changed notifications
   */
  isListChangedSupported(clientName) {
    if (!clientName) {
      return false;
    }
    const normalizedName = clientName.toLowerCase();
    return LIST_CHANGED_SUPPORTED_CLIENTS.some((supported) => normalizedName.includes(supported));
  }
  /**
   * Log client compatibility information
   */
  logClientCompatibility(clientName) {
    const isSupported = this.isListChangedSupported(clientName);
    if (!isSupported) {
    } else {
    }
  }
};

// src/unity-event-handler.ts
var UnityEventHandler = class {
  server;
  unityClient;
  connectionManager;
  isDevelopment;
  shuttingDown = false;
  isNotifying = false;
  constructor(server2, unityClient, connectionManager) {
    this.server = server2;
    this.unityClient = unityClient;
    this.connectionManager = connectionManager;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
  }
  /**
   * Setup Unity event listener for automatic tool updates
   */
  setupUnityEventListener(onToolsChanged) {
    this.unityClient.onNotification("notifications/tools/list_changed", (_params) => {
      VibeLogger.logInfo(
        "unity_notification_received",
        "Unity notification received: notifications/tools/list_changed",
        void 0,
        void 0,
        "Unity notified that tool list has changed"
      );
      try {
        void onToolsChanged();
      } catch (error) {
        VibeLogger.logError(
          "unity_notification_error",
          "Failed to update dynamic tools via Unity notification",
          { error: error instanceof Error ? error.message : String(error) },
          void 0,
          "Error occurred while processing Unity tool list change notification"
        );
      }
    });
  }
  /**
   * Send tools changed notification (with duplicate prevention)
   */
  sendToolsChangedNotification() {
    if (this.isNotifying) {
      if (this.isDevelopment) {
        VibeLogger.logDebug(
          "tools_notification_skipped",
          "sendToolsChangedNotification skipped: already notifying",
          void 0,
          void 0,
          "Duplicate notification prevented"
        );
      }
      return;
    }
    this.isNotifying = true;
    try {
      void this.server.notification({
        method: NOTIFICATION_METHODS.TOOLS_LIST_CHANGED,
        params: {}
      });
      if (this.isDevelopment) {
        VibeLogger.logInfo(
          "tools_notification_sent",
          "tools/list_changed notification sent",
          void 0,
          void 0,
          "Successfully notified client of tool list changes"
        );
      }
    } catch (error) {
      VibeLogger.logError(
        "tools_notification_error",
        "Failed to send tools changed notification",
        { error: error instanceof Error ? error.message : String(error) },
        void 0,
        "Error occurred while sending tool list change notification"
      );
    } finally {
      this.isNotifying = false;
    }
  }
  /**
   * Setup signal handlers for graceful shutdown
   */
  setupSignalHandlers() {
    process.on("SIGINT", () => {
      VibeLogger.logInfo(
        "sigint_received",
        "Received SIGINT, shutting down...",
        void 0,
        void 0,
        "User pressed Ctrl+C, initiating graceful shutdown"
      );
      this.gracefulShutdown();
    });
    process.on("SIGTERM", () => {
      VibeLogger.logInfo(
        "sigterm_received",
        "Received SIGTERM, shutting down...",
        void 0,
        void 0,
        "Process termination signal received, initiating graceful shutdown"
      );
      this.gracefulShutdown();
    });
    process.on("SIGHUP", () => {
      VibeLogger.logInfo(
        "sighup_received",
        "Received SIGHUP, shutting down...",
        void 0,
        void 0,
        "Terminal hangup signal received, initiating graceful shutdown"
      );
      this.gracefulShutdown();
    });
    process.stdin.on("close", () => {
      VibeLogger.logInfo(
        "stdin_closed",
        "STDIN closed, shutting down...",
        void 0,
        void 0,
        "Parent process disconnected, preventing orphaned process"
      );
      this.gracefulShutdown();
    });
    process.stdin.on("end", () => {
      VibeLogger.logInfo(
        "stdin_ended",
        "STDIN ended, shutting down...",
        void 0,
        void 0,
        "STDIN stream ended, initiating graceful shutdown"
      );
      this.gracefulShutdown();
    });
    process.on("uncaughtException", (error) => {
      VibeLogger.logException(
        "uncaught_exception",
        error,
        void 0,
        void 0,
        "Uncaught exception occurred, shutting down safely"
      );
      this.gracefulShutdown();
    });
    process.on("unhandledRejection", (reason, promise) => {
      VibeLogger.logError(
        "unhandled_rejection",
        "Unhandled promise rejection",
        { reason: String(reason), promise: String(promise) },
        void 0,
        "Unhandled promise rejection occurred, shutting down safely"
      );
      this.gracefulShutdown();
    });
  }
  /**
   * Graceful shutdown with proper cleanup
   * BUG FIX: Enhanced shutdown process to prevent orphaned Node processes
   */
  gracefulShutdown() {
    if (this.shuttingDown) {
      return;
    }
    this.shuttingDown = true;
    VibeLogger.logInfo(
      "graceful_shutdown_start",
      "Starting graceful shutdown...",
      void 0,
      void 0,
      "Initiating graceful shutdown process"
    );
    try {
      this.connectionManager.disconnect();
      if (global.gc) {
        global.gc();
      }
    } catch (error) {
      VibeLogger.logError(
        "cleanup_error",
        "Error during cleanup",
        { error: error instanceof Error ? error.message : String(error) },
        void 0,
        "Error occurred during graceful shutdown cleanup"
      );
    }
    VibeLogger.logInfo(
      "graceful_shutdown_complete",
      "Graceful shutdown completed",
      void 0,
      void 0,
      "All cleanup completed, process will exit"
    );
    process.exit(0);
  }
  /**
   * Check if shutdown is in progress
   */
  isShuttingDown() {
    return this.shuttingDown;
  }
};

// package.json
var package_default = {
  name: "uloopmcp-server",
  version: "0.30.1",
  description: "TypeScript MCP Server for Unity-Cursor integration",
  main: "dist/server.bundle.js",
  type: "module",
  scripts: {
    prepare: "husky",
    build: "npm run build:bundle",
    "build:bundle": "esbuild src/server.ts --bundle --platform=node --format=esm --outfile=dist/server.bundle.js --external:fs --external:path --external:net --external:os --sourcemap",
    "build:production": "cross-env ULOOPMCP_PRODUCTION=true NODE_ENV=production esbuild src/server.ts --bundle --platform=node --format=esm --outfile=dist/server.bundle.js --external:fs --external:path --external:net --external:os --sourcemap",
    dev: "cross-env NODE_ENV=development npm run build:bundle && cross-env NODE_ENV=development NODE_OPTIONS=--enable-source-maps node dist/server.bundle.js",
    "dev:watch": "cross-env NODE_ENV=development esbuild src/server.ts --bundle --platform=node --format=esm --outfile=dist/server.bundle.js --external:fs --external:path --external:net --external:os --sourcemap --watch",
    start: "cross-env NODE_OPTIONS=--enable-source-maps node dist/server.bundle.js",
    "start:production": "cross-env ULOOPMCP_PRODUCTION=true NODE_OPTIONS=--enable-source-maps node dist/server.bundle.js",
    "start:dev": "cross-env NODE_ENV=development NODE_OPTIONS=--enable-source-maps node dist/server.bundle.js",
    lint: "eslint src --ext .ts",
    "lint:fix": "eslint src --ext .ts --fix",
    "security:check": "eslint src --ext .ts",
    "security:fix": "eslint src --ext .ts --fix",
    "security:only": "eslint src --ext .ts --config .eslintrc.security.json",
    "security:sarif": "eslint src --ext .ts -f @microsoft/eslint-formatter-sarif -o security-results.sarif",
    "security:sarif-only": "eslint src --ext .ts --config .eslintrc.security.json -f @microsoft/eslint-formatter-sarif -o typescript-security.sarif --max-warnings 0",
    format: "prettier --write src/**/*.ts",
    "format:check": "prettier --check src/**/*.ts",
    "lint:check": "npm run lint && npm run format:check",
    test: "jest",
    "test:mcp": "tsx src/tools/__tests__/test-runner.ts",
    "test:integration": "tsx src/tools/__tests__/integration-test.ts",
    "test:watch": "jest --watch",
    validate: "npm run test:integration && echo 'Integration tests passed - safe to deploy'",
    deploy: "npm run validate && npm run build",
    "debug:compile": "tsx debug/compile-check.ts",
    "debug:logs": "tsx debug/logs-fetch.ts",
    "debug:connection": "tsx debug/connection-check.ts",
    "debug:all-logs": "tsx debug/all-logs-fetch.ts",
    "debug:compile-detailed": "tsx debug/compile-detailed.ts",
    "debug:connection-survival": "tsx debug/connection-survival.ts",
    "debug:domain-reload-timing": "tsx debug/domain-reload-timing.ts",
    "debug:event-test": "tsx debug/event-test.ts",
    "debug:notification-test": "tsx debug/notification-test.ts",
    prepublishOnly: "npm run build",
    postinstall: "npm run build"
  },
  "lint-staged": {
    "src/**/*.{ts,js}": [
      "eslint --fix",
      "prettier --write"
    ]
  },
  keywords: [
    "mcp",
    "unity",
    "cursor",
    "typescript"
  ],
  author: "hatayama",
  license: "MIT",
  dependencies: {
    "@modelcontextprotocol/sdk": "1.12.2",
    "@types/uuid": "^10.0.0",
    uuid: "^11.1.0",
    zod: "3.25.64"
  },
  devDependencies: {
    "@microsoft/eslint-formatter-sarif": "^3.1.0",
    "@types/jest": "^29.5.14",
    "@types/node": "20.19.0",
    "@typescript-eslint/eslint-plugin": "^7.18.0",
    "@typescript-eslint/parser": "^7.18.0",
    "cross-env": "^10.0.0",
    esbuild: "^0.25.6",
    eslint: "^8.57.1",
    "eslint-config-prettier": "^9.1.0",
    "eslint-plugin-prettier": "^5.2.1",
    "eslint-plugin-security": "^3.0.1",
    husky: "^9.0.0",
    jest: "^30.0.0",
    "lint-staged": "^15.0.0",
    prettier: "^3.5.0",
    "ts-jest": "^29.4.0",
    tsx: "4.20.3",
    typescript: "5.8.3"
  }
};

// src/server.ts
var UnityMcpServer = class {
  server;
  unityClient;
  isDevelopment;
  isInitialized = false;
  unityDiscovery;
  connectionManager;
  toolManager;
  clientCompatibility;
  eventHandler;
  constructor() {
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
    this.server = new Server(
      {
        name: MCP_SERVER_NAME,
        version: package_default.version
      },
      {
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY
          }
        }
      }
    );
    this.unityClient = UnityClient.getInstance();
    this.connectionManager = new UnityConnectionManager(this.unityClient);
    this.unityDiscovery = this.connectionManager.getUnityDiscovery();
    this.toolManager = new UnityToolManager(this.unityClient);
    this.toolManager.setConnectionManager(this.connectionManager);
    this.clientCompatibility = new McpClientCompatibility(this.unityClient);
    this.eventHandler = new UnityEventHandler(
      this.server,
      this.unityClient,
      this.connectionManager
    );
    this.connectionManager.setupReconnectionCallback(async () => {
      await this.toolManager.refreshDynamicToolsSafe(() => {
        this.eventHandler.sendToolsChangedNotification();
      });
    });
    this.setupHandlers();
    this.eventHandler.setupSignalHandlers();
  }
  /**
   * Initialize client synchronously (for list_changed unsupported clients)
   */
  async initializeSyncClient(clientName) {
    try {
      await this.clientCompatibility.initializeClient(clientName);
      this.toolManager.setClientName(clientName);
      await this.connectionManager.waitForUnityConnectionWithTimeout(1e4);
      const tools = await this.toolManager.getToolsFromUnity();
      return {
        protocolVersion: MCP_PROTOCOL_VERSION,
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY
          }
        },
        serverInfo: {
          name: MCP_SERVER_NAME,
          version: package_default.version
        },
        tools
      };
    } catch (error) {
      VibeLogger.logError(
        "mcp_unity_connection_timeout",
        "Unity connection timeout",
        {
          client_name: clientName,
          error_message: error instanceof Error ? error.message : String(error)
        },
        void 0,
        "Unity connection timed out - check Unity MCP bridge status"
      );
      return {
        protocolVersion: MCP_PROTOCOL_VERSION,
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY
          }
        },
        serverInfo: {
          name: MCP_SERVER_NAME,
          version: package_default.version
        },
        tools: []
      };
    }
  }
  /**
   * Initialize client asynchronously (for list_changed supported clients)
   */
  initializeAsyncClient(clientName) {
    void this.clientCompatibility.initializeClient(clientName);
    this.toolManager.setClientName(clientName);
    void this.toolManager.initializeDynamicTools().then(() => {
    }).catch((error) => {
      VibeLogger.logError(
        "mcp_unity_connection_init_failed",
        "Unity connection initialization failed",
        { error_message: error instanceof Error ? error.message : String(error) },
        void 0,
        "Unity connection could not be established - check Unity MCP bridge"
      );
      this.unityDiscovery.start();
    });
  }
  setupHandlers() {
    this.server.setRequestHandler(InitializeRequestSchema, async (request) => {
      const clientInfo = request.params?.clientInfo;
      const clientName = clientInfo?.name || "";
      if (clientName) {
        this.clientCompatibility.setupClientCompatibility(clientName);
      }
      if (!this.isInitialized) {
        this.isInitialized = true;
        if (this.clientCompatibility.isListChangedUnsupported(clientName)) {
          return this.initializeSyncClient(clientName);
        } else {
          this.initializeAsyncClient(clientName);
        }
      }
      return {
        protocolVersion: MCP_PROTOCOL_VERSION,
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY
          }
        },
        serverInfo: {
          name: MCP_SERVER_NAME,
          version: package_default.version
        }
      };
    });
    this.server.setRequestHandler(ListToolsRequestSchema, () => {
      const tools = this.toolManager.getAllTools();
      return { tools };
    });
    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params;
      try {
        if (this.toolManager.hasTool(name)) {
          const dynamicTool = this.toolManager.getTool(name);
          if (!dynamicTool) {
            throw new Error(`Tool ${name} is not available`);
          }
          const result = await dynamicTool.execute(args ?? {});
          return {
            content: result.content,
            isError: result.isError
          };
        }
        throw new Error(`Unknown tool: ${name}`);
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Error executing ${name}: ${error instanceof Error ? error.message : "Unknown error"}`
            }
          ],
          isError: true
        };
      }
    });
  }
  /**
   * Start the server
   */
  async start() {
    this.eventHandler.setupUnityEventListener(async () => {
      await this.toolManager.refreshDynamicToolsSafe(() => {
        this.eventHandler.sendToolsChangedNotification();
      });
    });
    this.connectionManager.initialize(async () => {
      const clientName = this.clientCompatibility.getClientName();
      if (clientName) {
        this.toolManager.setClientName(clientName);
        await this.toolManager.initializeDynamicTools();
        this.eventHandler.sendToolsChangedNotification();
      } else {
      }
    });
    if (this.isDevelopment) {
    }
    const transport = new StdioServerTransport();
    await this.server.connect(transport);
  }
};
var server = new UnityMcpServer();
server.start().catch((error) => {
  VibeLogger.logError(
    "mcp_server_startup_fatal",
    "Unity MCP Server startup failed",
    {
      error_message: error instanceof Error ? error.message : String(error),
      stack_trace: error instanceof Error ? error.stack : "No stack trace available",
      error_type: error instanceof Error ? error.constructor.name : typeof error
    },
    void 0,
    "Fatal server startup error - check Unity MCP bridge configuration"
  );
  process.exit(1);
});
//# sourceMappingURL=server.bundle.js.map
