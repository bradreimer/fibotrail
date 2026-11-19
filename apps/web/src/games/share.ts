/** How sharing went, for the button's label. */
export type ShareOutcome = 'shared' | 'copied' | 'failed';

/**
 * Shares a game's result text: with the system share sheet when the browser has one, as on an
 * iPhone, and otherwise by copying it. The text holds no trip name, place, date, or photo.
 */
export async function shareResult(text: string): Promise<ShareOutcome> {
  if (typeof navigator.share === 'function') {
    try {
      await navigator.share({ text });
      return 'shared';
    } catch (error) {
      // Closing the share sheet isn't a failure, and there's nothing to copy then.
      if (error instanceof DOMException && error.name === 'AbortError') return 'failed';
    }
  }
  try {
    await navigator.clipboard.writeText(text);
    return 'copied';
  } catch {
    return 'failed';
  }
}
