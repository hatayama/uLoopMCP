import { BaseTool } from './base-tool.js';
import { ToolContext, ToolResponse } from '../types/tool-types.js';
import { PARAMETER_SCHEMA } from '../constants.js';
import * as fs from 'fs';
import * as path from 'path';
import { VibeLogger } from '../utils/vibe-logger.js';

// Type definitions for Unity parameter schema
interface UnityParameterInfo {
  [key: string]: unknown;
}

interface UnityParameterSchema {
  [key: string]: unknown;
}

interface JsonSchemaProperty {
  type: string;
  description?: string;
  default?: unknown;
  enum?: string[];
  items?: { type: string };
}

interface InputSchema {
  type: string;
  properties: Record<string, JsonSchemaProperty>;
  required?: string[];
  additionalProperties?: boolean;
}

interface CompileExecutionContext {
  shouldWaitForDomainReload: boolean;
  requestId?: string;
}

/**
 * Dynamically generated tool for Unity tools
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityMcpServer: Instantiates and uses this tool
 * - UnityClient: Used to execute the actual tool in Unity
 * - BaseTool: Base class providing common tool functionality
 */
export class DynamicUnityCommandTool extends BaseTool {
  private static readonly DOMAIN_RELOAD_WAIT_TIMEOUT_MS = 10000;
  private static readonly DOMAIN_RELOAD_WAIT_POLL_INTERVAL_MS = 100;

  public readonly name: string;
  public readonly description: string;
  public readonly inputSchema: InputSchema;
  private readonly toolName: string;

  constructor(
    context: ToolContext,
    toolName: string,
    description: string,
    parameterSchema?: UnityParameterSchema,
  ) {
    super(context);
    this.toolName = toolName;
    this.name = toolName;
    this.description = description;
    this.inputSchema = this.generateInputSchema(parameterSchema);
  }

  private generateInputSchema(parameterSchema?: UnityParameterSchema): InputSchema {
    if (this.hasNoParameters(parameterSchema)) {
      // For tools without parameters, return minimal schema without dummy parameters
      return {
        type: 'object',
        properties: {},
        additionalProperties: false,
      };
    }

    const properties: Record<string, JsonSchemaProperty> = {};
    const required: string[] = [];

    // Convert Unity parameter schema to JSON Schema format using constants
    if (!parameterSchema) {
      throw new Error('Parameter schema is undefined');
    }
    const propertiesObj = parameterSchema[PARAMETER_SCHEMA.PROPERTIES_PROPERTY] as Record<
      string,
      UnityParameterInfo
    >;
    for (const [propName, propInfo] of Object.entries(propertiesObj)) {
      const info = propInfo;

      const typeValue = info[PARAMETER_SCHEMA.TYPE_PROPERTY];
      const descriptionValue = info[PARAMETER_SCHEMA.DESCRIPTION_PROPERTY];

      const property: JsonSchemaProperty = {
        type: this.convertType(typeof typeValue === 'string' ? typeValue : 'string'),
        description:
          typeof descriptionValue === 'string' ? descriptionValue : `Parameter: ${propName}`,
      };

      // Add default value if provided
      const defaultValue = info[PARAMETER_SCHEMA.DEFAULT_VALUE_PROPERTY];
      if (defaultValue !== undefined && defaultValue !== null) {
        property.default = defaultValue;
      }

      // Add enum values if provided using constants
      const enumValues = info[PARAMETER_SCHEMA.ENUM_PROPERTY];
      if (enumValues && Array.isArray(enumValues) && enumValues.length > 0) {
        property.enum = enumValues as string[];
      }

      // Handle array type
      if (
        info[PARAMETER_SCHEMA.TYPE_PROPERTY] === 'array' &&
        defaultValue &&
        Array.isArray(defaultValue)
      ) {
        property.items = {
          type: 'string',
        };
        property.default = defaultValue;
      }

      // SECURITY: propName is validated input from Unity's tool schema - safe for object property assignment
      Object.defineProperty(properties, propName, {
        value: property,
        writable: true,
        enumerable: true,
        configurable: true,
      });
    }

    // Add required parameters using constants
    if (!parameterSchema) {
      throw new Error('Parameter schema is undefined');
    }
    const requiredParams = parameterSchema[PARAMETER_SCHEMA.REQUIRED_PROPERTY];
    if (requiredParams && Array.isArray(requiredParams)) {
      required.push(...(requiredParams as string[]));
    }

    const schema = {
      type: 'object',
      properties: properties,
      required: required.length > 0 ? required : undefined,
    };

    return schema;
  }

  private convertType(unityType: string): string {
    switch (unityType?.toLowerCase()) {
      case 'string':
        return 'string';
      case 'number':
      case 'int':
      case 'float':
      case 'double':
        return 'number';
      case 'boolean':
      case 'bool':
        return 'boolean';
      case 'array':
        return 'array';
      default:
        return 'string'; // Default fallback
    }
  }

  private hasNoParameters(parameterSchema?: UnityParameterSchema): boolean {
    if (!parameterSchema) {
      return true;
    }

    const properties = parameterSchema[PARAMETER_SCHEMA.PROPERTIES_PROPERTY];
    if (!properties || typeof properties !== 'object') {
      return true;
    }

    return Object.keys(properties).length === 0;
  }

  validateArgs(args: unknown): Record<string, unknown> {
    // If no real parameters are defined, return empty object
    if (!this.inputSchema.properties || Object.keys(this.inputSchema.properties).length === 0) {
      return {};
    }

    return (args as Record<string, unknown>) || {};
  }

  async execute(args: Record<string, unknown>): Promise<ToolResponse> {
    try {
      // Validate and use the provided arguments
      const actualArgs: Record<string, unknown> = this.validateArgs(args);
      const compileContext = this.prepareCompileExecutionContext(actualArgs);

      let immediateResult: unknown;
      let executionError: unknown = undefined;
      try {
        immediateResult = await this.context.unityClient.executeTool(this.toolName, actualArgs);
      } catch (error) {
        immediateResult = undefined;
        executionError = error;
      }

      if (!compileContext.shouldWaitForDomainReload) {
        if (executionError !== undefined) {
          throw executionError;
        }

        return {
          content: [
            {
              type: 'text',
              text:
                typeof immediateResult === 'string'
                  ? immediateResult
                  : JSON.stringify(immediateResult, null, 2),
            },
          ],
          isError: this.isUnityFailureResult(immediateResult),
        };
      }

      const storedResult = await this.waitForStoredCompileResult(compileContext.requestId ?? '');
      const finalResult = storedResult ?? immediateResult;
      if (finalResult === undefined && executionError !== undefined) {
        throw executionError;
      }

      if (finalResult === undefined) {
        throw new Error(
          'Compile result was unavailable after domain reload. Run compile again or use get-logs.',
        );
      }

      return {
        content: [
          {
            type: 'text',
            text: typeof finalResult === 'string' ? finalResult : JSON.stringify(finalResult, null, 2),
          },
        ],
        // Treat Unity-side failure responses as MCP tool errors.
        // Otherwise tools that return { Success: false, ... } appear as "success" in MCP Inspector.
        isError: this.isUnityFailureResult(finalResult),
      };
    } catch (error) {
      return this.formatErrorResponse(error);
    }
  }

  private prepareCompileExecutionContext(actualArgs: Record<string, unknown>): CompileExecutionContext {
    if (this.toolName !== 'compile') {
      return { shouldWaitForDomainReload: false };
    }

    const forceRecompile = this.toBoolean(actualArgs['ForceRecompile']);
    const waitForDomainReload = this.toBoolean(actualArgs['WaitForDomainReload']);
    const shouldWaitForDomainReload = forceRecompile && waitForDomainReload;

    if (!shouldWaitForDomainReload) {
      return { shouldWaitForDomainReload: false };
    }

    const requestId = this.ensureCompileRequestId(actualArgs);
    return {
      shouldWaitForDomainReload: true,
      requestId,
    };
  }

  private toBoolean(value: unknown): boolean {
    if (typeof value === 'boolean') {
      return value;
    }

    if (typeof value === 'string') {
      return value.toLowerCase() === 'true';
    }

    return false;
  }

  private ensureCompileRequestId(actualArgs: Record<string, unknown>): string {
    const existingRequestId = actualArgs['RequestId'];
    if (typeof existingRequestId === 'string' && existingRequestId.length > 0) {
      return existingRequestId;
    }

    const requestId = this.createCompileRequestId();
    actualArgs['RequestId'] = requestId;
    return requestId;
  }

  private createCompileRequestId(): string {
    const timestamp = Date.now();
    const randomToken = Math.floor(Math.random() * 1000000)
      .toString()
      .padStart(6, '0');
    return `compile_${timestamp}_${randomToken}`;
  }

  private async waitForStoredCompileResult(requestId: string): Promise<unknown | undefined> {
    const projectRoot = VibeLogger.getProjectRoot();
    const resultFilePath = this.getCompileResultFilePath(projectRoot, requestId);

    let waitedMs = 0;
    let busyObserved = false;

    while (waitedMs < DynamicUnityCommandTool.DOMAIN_RELOAD_WAIT_TIMEOUT_MS) {
      const isBusy = this.isUnityBusyByLockFiles(projectRoot);
      if (isBusy) {
        busyObserved = true;
      }

      const storedResult = this.tryReadCompileResult(resultFilePath);
      if (storedResult !== undefined && busyObserved && !isBusy) {
        return storedResult;
      }

      await this.sleep(DynamicUnityCommandTool.DOMAIN_RELOAD_WAIT_POLL_INTERVAL_MS);
      waitedMs += DynamicUnityCommandTool.DOMAIN_RELOAD_WAIT_POLL_INTERVAL_MS;
    }

    if (!busyObserved) {
      throw new Error(
        `Domain reload was not detected within ${DynamicUnityCommandTool.DOMAIN_RELOAD_WAIT_TIMEOUT_MS}ms for request '${requestId}'.`,
      );
    }

    throw new Error(
      `Domain reload wait timed out after ${DynamicUnityCommandTool.DOMAIN_RELOAD_WAIT_TIMEOUT_MS}ms for request '${requestId}'.`,
    );
  }

  private isUnityBusyByLockFiles(projectRoot: string): boolean {
    const compilingLockPath = path.join(projectRoot, 'Temp', 'compiling.lock');
    if (fs.existsSync(compilingLockPath)) {
      return true;
    }

    const domainReloadLockPath = path.join(projectRoot, 'Temp', 'domainreload.lock');
    if (fs.existsSync(domainReloadLockPath)) {
      return true;
    }

    const serverStartingLockPath = path.join(projectRoot, 'Temp', 'serverstarting.lock');
    return fs.existsSync(serverStartingLockPath);
  }

  private getCompileResultFilePath(projectRoot: string, requestId: string): string {
    return path.join(projectRoot, 'Temp', 'uLoopMCP', 'compile-results', `${requestId}.json`);
  }

  private tryReadCompileResult(resultFilePath: string): unknown | undefined {
    if (!fs.existsSync(resultFilePath)) {
      return undefined;
    }

    const content = fs.readFileSync(resultFilePath, 'utf-8');
    return JSON.parse(this.stripUtf8Bom(content)) as unknown;
  }

  private stripUtf8Bom(content: string): string {
    if (content.charCodeAt(0) === 0xfeff) {
      return content.slice(1);
    }

    return content;
  }

  private sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  private isUnityFailureResult(result: unknown): boolean {
    if (typeof result !== 'object' || result === null) {
      return false;
    }

    const obj = result as Record<string, unknown>;

    // Convention across Unity tools: Success=false means tool-level failure.
    if (obj.Success === false) {
      return true;
    }

    return false;
  }

  protected formatErrorResponse(error: unknown): ToolResponse {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    return {
      content: [
        {
          type: 'text',
          text: errorMessage,
        },
      ],
      isError: true,
    };
  }
}
