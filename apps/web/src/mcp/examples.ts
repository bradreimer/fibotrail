import data from './examples.json';

/** A question that works with the MCP server, and the tools a model calls to answer it. */
export interface Example {
  id: string;
  group: string;
  question: string;
  /** What the answer holds, in plain words. */
  answer: string;
  calls: readonly { tool: string; arguments: Record<string, unknown> }[];
}

/** A question the server can't answer, and why. */
export interface Limit {
  question: string;
  why: string;
}

/**
 * The example questions. They live in `examples.json`, and a server test runs every call of every
 * example against a made-up library, so an example never promises a tool that is missing. See
 * docs/specs/mcp-server.md.
 */
export const EXAMPLES: readonly Example[] = data.examples;

export const LIMITS: readonly Limit[] = data.cannot;

/** The groups in the order of their first example. */
export function exampleGroups(): readonly { name: string; examples: readonly Example[] }[] {
  const names = [...new Set(EXAMPLES.map((example) => example.group))];
  return names.map((name) => ({
    name,
    examples: EXAMPLES.filter((example) => example.group === name),
  }));
}

/** The distinct tool names an example uses, in call order. */
export function toolsOf(example: Example): readonly string[] {
  return [...new Set(example.calls.map((call) => call.tool))];
}
