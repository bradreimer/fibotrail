import { useSession } from '../api/session';
import { Skeleton } from '../states/Skeleton';

export function SettingsScreen() {
  const session = useSession();
  return (
    <div className="settings">
      <section className="settings__section" aria-labelledby="settings-account">
        <h2 className="state__title" id="settings-account">
          Account
        </h2>
        {session ? (
          // A plain form post: the server ends the session and answers with the sign-in screen.
          <form method="post" action="/sign-out">
            <input type="hidden" name="csrf" value={session.csrfToken} />
            <button type="submit" className="button button--secondary">
              Sign out
            </button>
          </form>
        ) : (
          <Skeleton count={1} label="Loading your account" />
        )}
        <p className="settings__hint">
          Signing out ends your session on this device. Your trips stay as they are.
        </p>
      </section>
    </div>
  );
}
