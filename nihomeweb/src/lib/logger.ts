export type LogSeverity = "INFO" | "DEBUG" | "WARNING" | "ERROR" | "CRITICAL";

type LogContext = {
  apiName?: string;
  correlationId?: string;
  messageType?: string;
  error?: unknown;
};

const config = __NIHOME_LOGGING_CONFIG__;

function createIdentifier(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (character) => {
    const random = (Math.random() * 16) | 0;
    const value = character === "x" ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}

const browserProcessId = `browser-${createIdentifier()}`;
const browserCorrelationId = createIdentifier();

const describeError = (error: unknown): string => {
  if (error instanceof Error) {
    return `${error.name}: ${error.message}${error.stack ? ` | ${error.stack}` : ""}`;
  }
  if (typeof error === "string") return error;
  try {
    return JSON.stringify(error);
  } catch {
    return String(error);
  }
};

const write = (
  source: string,
  severity: LogSeverity,
  message: string,
  context: LogContext = {},
) => {
  const date = new Date().toISOString();
  const errorSuffix = context.error === undefined ? "" : ` | ${describeError(context.error)}`;
  const record = {
    logger: source,
    message: `date="${date}", process_id="${browserProcessId}", ${message}${errorSuffix}`,
    properties: {
      api_name: context.apiName ?? config.ApiName,
      app_name: config.AppName,
      correlation_id: context.correlationId ?? browserCorrelationId,
      message_type: context.messageType ?? config.MessageType,
    },
    severity,
    thread: config.ThreadName,
  };

  const serialized = JSON.stringify(record);
  if (severity === "ERROR" || severity === "CRITICAL") {
    console.error(serialized);
  } else if (severity === "WARNING") {
    console.warn(serialized);
  } else if (severity === "DEBUG") {
    console.debug(serialized);
  } else {
    console.info(serialized);
  }
};

export const createLogger = (source: string) => ({
  debug: (message: string, context?: LogContext) => write(source, "DEBUG", message, context),
  info: (message: string, context?: LogContext) => write(source, "INFO", message, context),
  warning: (message: string, context?: LogContext) => write(source, "WARNING", message, context),
  error: (message: string, context?: LogContext) => write(source, "ERROR", message, context),
  critical: (message: string, context?: LogContext) => write(source, "CRITICAL", message, context),
});

export const newCorrelationId = createIdentifier;
