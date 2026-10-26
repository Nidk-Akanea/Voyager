/** What to write in a log or an error message about something that was thrown: its message, or its text. */
export function describeError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
