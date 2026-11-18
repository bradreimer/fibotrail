import { useState } from 'react';

interface CopyButtonProps {
  text: string;
  /** What the button copies, for a screen reader: "Copy the server address". */
  label: string;
}

/** Copies text to the clipboard, and says whether it worked. */
export function CopyButton({ text, label }: CopyButtonProps) {
  const [result, setResult] = useState<'idle' | 'copied' | 'failed'>('idle');

  async function copy() {
    try {
      await navigator.clipboard.writeText(text);
      setResult('copied');
    } catch {
      setResult('failed');
    }
  }

  return (
    <span className="mcp__copy">
      <button type="button" className="button button--secondary" onClick={() => void copy()}>
        <span aria-hidden="true">Copy</span>
        <span className="visually-hidden">{label}</span>
      </button>
      <span role="status" className="mcp__copied">
        {result === 'copied' && 'Copied'}
        {result === 'failed' && "Couldn't copy. Select the text and copy it."}
      </span>
    </span>
  );
}
