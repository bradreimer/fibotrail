import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { CopyButton } from '../mcp/CopyButton';
import {
  ENV_LINE,
  MAKE_TOKEN_COMMAND,
  RESTART_COMMAND,
  TOKEN_PLACEHOLDER,
  bridgeConfig,
  claudeCodeCommand,
  isHomeAddress,
  serverAddress,
  urlConfig,
} from '../mcp/connect';
import { EXAMPLES, LIMITS, exampleGroups, toolsOf } from '../mcp/examples';
import { ErrorState } from '../states/ErrorState';

function CodeBlock({ text, label }: { text: string; label: string }) {
  return (
    <div className="mcp__block">
      <pre className="mcp__code" aria-label={label}>
        <code>{text}</code>
      </pre>
      <CopyButton text={text} label={`Copy ${label.toLowerCase()}`} />
    </div>
  );
}

function TurnOn() {
  return (
    <section className="settings__section" aria-labelledby="mcp-turn-on">
      <h2 className="state__title" id="mcp-turn-on">
        Turn it on
      </h2>
      <p className="settings__status" role="status">
        <span className="settings__dot settings__dot--off" aria-hidden="true" />
        MCP server: off
      </p>
      <p>
        The MCP server is off until you give it a token. The token is a password for your AI client,
        and it lives only in your <code>.env</code> file.
      </p>
      <ol className="mcp__steps">
        <li>
          Make a token, at least 32 characters:
          <CodeBlock text={MAKE_TOKEN_COMMAND} label="the command that makes a token" />
        </li>
        <li>
          Add this line to your <code>.env</code> file, with your token in place of the words:
          <CodeBlock text={ENV_LINE} label="the .env line" />
        </li>
        <li>
          Restart Fibotrail, so it reads the new line:
          <CodeBlock text={RESTART_COMMAND} label="the restart command" />
        </li>
      </ol>
      <p className="settings__hint">Then come back to this screen. It shows how to connect.</p>
    </section>
  );
}

function Connect({ address, home }: { address: string; home: boolean }) {
  return (
    <section className="settings__section" aria-labelledby="mcp-connect">
      <h2 className="state__title" id="mcp-connect">
        Connect an AI client
      </h2>
      <p className="settings__status" role="status">
        <span className="settings__dot" aria-hidden="true" />
        MCP server: on
      </p>
      <h3 className="mcp__subhead">1. The address</h3>
      <CodeBlock text={address} label="the server address" />
      {home && (
        <p className="settings__hint" role="note">
          This address works only on your own network. An app that runs in the cloud can't reach it.
          Open Fibotrail at the https address of your reverse proxy to see the address to use.
        </p>
      )}
      <h3 className="mcp__subhead">2. The token</h3>
      <p>
        Your token is the <code>MCP_TOKEN</code> value in your <code>.env</code> file. Fibotrail
        never shows it here. In the steps below, replace <code>{TOKEN_PLACEHOLDER}</code> with it.
      </p>
      <h3 className="mcp__subhead">3. Add Fibotrail to your client</h3>
      <details className="mcp__client">
        <summary>Claude Code</summary>
        <p>Run this in a terminal:</p>
        <CodeBlock text={claudeCodeCommand(address)} label="the Claude Code command" />
      </details>
      <details className="mcp__client">
        <summary>Claude Desktop, and apps that start a local command</summary>
        <p>
          Add this to the app's MCP settings file. <code>mcp-remote</code> needs Node.js. It carries
          the token for apps that can't send a header themselves.
        </p>
        <CodeBlock
          text={bridgeConfig(address)}
          label="the settings for an app that starts a command"
        />
      </details>
      <details className="mcp__client">
        <summary>Any client that takes an address and headers</summary>
        <p>Add this to the client's MCP settings:</p>
        <CodeBlock text={urlConfig(address)} label="the settings for an app that takes headers" />
      </details>
      <p className="settings__hint">
        Menus change from one release of a client to the next. If a step doesn't match what you see,
        look for "MCP" or "connectors" in the client's own help.
      </p>
    </section>
  );
}

function Examples() {
  return (
    <section className="settings__section" aria-labelledby="mcp-examples">
      <h2 className="state__title" id="mcp-examples">
        Questions to try
      </h2>
      <p className="settings__hint">
        Each of these works. Ask in your own words, and the AI client picks the tools.
      </p>
      {exampleGroups().map((group) => (
        <div key={group.name} className="mcp__group">
          <h3 className="mcp__subhead">{group.name}</h3>
          <ul className="mcp__examples">
            {group.examples.map((example) => (
              <li key={example.id} className="mcp__example">
                <p className="mcp__question">{example.question}</p>
                <p className="settings__hint">
                  {example.answer} Uses{' '}
                  {toolsOf(example).map((tool, index) => (
                    <span key={tool}>
                      {index > 0 && ', '}
                      <code>{tool}</code>
                    </span>
                  ))}
                  .
                </p>
                <CopyButton
                  text={example.question}
                  label={`Copy the question: ${example.question}`}
                />
              </li>
            ))}
          </ul>
        </div>
      ))}
    </section>
  );
}

function Limits() {
  return (
    <section className="settings__section" aria-labelledby="mcp-limits">
      <h2 className="state__title" id="mcp-limits">
        What it can't answer
      </h2>
      <ul className="mcp__examples">
        {LIMITS.map((limit) => (
          <li key={limit.question} className="mcp__example">
            <p className="mcp__question">{limit.question}</p>
            <p className="settings__hint">{limit.why}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}

function Privacy() {
  return (
    <section className="settings__section" aria-labelledby="mcp-privacy">
      <h2 className="state__title" id="mcp-privacy">
        What your AI client sees
      </h2>
      <p>
        Only counts, dates, distances in kilometers, and the names of countries, continents,
        collections, and parks. Never photos, coordinates, trip names, or city names. The server
        only reads: it can't change a trip, and it never calls Immich.
      </p>
      <p>
        If your client uses a model in the cloud, those answers go to that model. To keep them at
        home, connect a client that uses a model on your own computer.
      </p>
    </section>
  );
}

/**
 * The MCP screen: how to turn the MCP server on, how to connect an AI client to it, and questions
 * that work. See docs/specs/mcp-server.md.
 */
export function McpScreen() {
  const status = useApi('/api/mcp/status');

  if (status.loading && !status.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the MCP screen</span>
        <span className="skeleton skeleton--text" aria-hidden="true" />
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (status.error) return <ErrorState message={status.error} onRetry={status.reload} />;
  if (!status.data) return null;

  const origin = window.location.origin;
  return (
    <div className="settings mcp">
      <p>
        Ask questions about your travel in plain words, from an AI client such as Claude. For
        example: "{EXAMPLES[0]?.question}"
      </p>
      {status.data.enabled ? (
        <Connect address={serverAddress(origin)} home={isHomeAddress(origin)} />
      ) : (
        <TurnOn />
      )}
      <Examples />
      <Limits />
      <Privacy />
      <p className="settings__hint">
        <Link to="/settings">Back to Settings</Link>
      </p>
    </div>
  );
}
