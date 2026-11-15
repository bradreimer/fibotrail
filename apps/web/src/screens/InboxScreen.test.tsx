import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { App } from '../App';
import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import { suggestionSentence, SWIPE_DISTANCE, type Suggestion } from '../inbox/SuggestionCard';
import { fakeServer, status } from '../test/server';
import { installMatchMedia } from '../test/window';
import { formatDateRange } from '../trips/dates';
import { projectRoute } from '../trips/RoutePreview';

const vancouver = {
  latitude: 49.25,
  longitude: -123.1,
  name: 'Vancouver, British Columbia, Canada',
};
const homeSet = { confirmed: vancouver, suggestion: null, hasPositions: true, photoCount: 5200 };

function card(
  id: number,
  place: string,
  start: string,
  end: string,
  more: Partial<Suggestion> = {},
): Suggestion {
  return {
    id,
    status: 'suggested',
    name: `${place}, March 2027`,
    place,
    places: [place, 'Kissimmee'],
    startDate: start,
    endDate: end,
    photoCount: 312,
    confidence: 'high',
    coverPhotoId: `cover-${id}`,
    strip: [
      { id: `p${id}-1`, alt: `Photo taken Mar 15, 2027 in ${place}`, isVideo: false },
      { id: `p${id}-2`, alt: `Photo taken Mar 16, 2027 in ${place}`, isVideo: false },
    ],
    route: [
      { latitude: 28.4, longitude: -81.55, photoCount: 200 },
      { latitude: 28.5, longitude: -81.4, photoCount: 112 },
    ],
    ...more,
  };
}

const orlando = card(1, 'Orlando', '2027-03-15', '2027-03-24');
const lakeside = card(2, 'Lakeside', '2027-07-03', '2027-07-06', {
  confidence: 'medium',
  photoCount: 60,
});
const paris = card(3, 'Paris', '2027-09-01', '2027-09-06', {
  status: 'later',
  confidence: 'low',
  photoCount: 22,
});

/** A stand-in server that keeps an inbox and applies decisions to it, as the real one does. */
function inboxServer(cards: Suggestion[], extra: Record<string, unknown> = {}) {
  let all = cards.map((item) => ({ ...item }));
  const inbox = () => ({
    suggestions: [
      ...all.filter((item) => item.status === 'suggested'),
      ...all.filter((item) => item.status === 'later'),
    ],
    ignored: all.filter((item) => item.status === 'ignored'),
    count: all.filter((item) => item.status === 'suggested').length,
  });
  const routes: Record<string, unknown> = {
    'GET /api/home': homeSet,
    'GET /api/inbox': () => inbox(),
    'GET /api/inbox/count': () => ({ count: inbox().count }),
    ...extra,
  };
  for (const item of cards) {
    routes[`GET /api/trips/${item.id}/photos`] = [
      { id: `cover-${item.id}`, alt: 'Photo taken Mar 15, 2027 in Orlando', isVideo: false },
      { id: `other-${item.id}`, alt: 'Photo taken Mar 18, 2027 in Orlando', isVideo: false },
    ];
    routes[`POST /api/trips/${item.id}/decision`] ??= (init?: RequestInit) => {
      const { action } = JSON.parse(init!.body as string) as { action: string };
      const next = { add: 'added', ignore: 'ignored', later: 'later', restore: 'suggested' }[
        action
      ]!;
      all = all.map((other) =>
        other.id === item.id ? { ...other, status: next as Suggestion['status'] } : other,
      );
      return status(204);
    };
  }
  return fakeServer(routes);
}

function renderInbox() {
  return render(
    <MemoryRouter initialEntries={['/inbox']}>
      <App />
    </MemoryRouter>,
  );
}

const sentence = (suggestion: Suggestion) => suggestionSentence(suggestion);

beforeEach(() => installMatchMedia(390));

describe('the suggestion card', () => {
  it('reads like the design document', () => {
    expect(sentence(orlando)).toBe(
      'We found a possible trip: Orlando, Mar 15–24, 2027. 312 photos.',
    );
    expect(sentence({ ...orlando, place: null, photoCount: 1 })).toBe(
      'We found a possible trip: Mar 15–24, 2027. 1 photo.',
    );
  });

  it('shows the places, the confidence as an icon and a word, the photo strip, and the route preview', async () => {
    inboxServer([orlando]);
    renderInbox();
    const article = (await screen.findByRole('heading', { name: sentence(orlando) })).closest(
      'article',
    )!;
    const view = within(article);

    expect(view.getByText('Orlando · Kissimmee')).toBeInTheDocument();
    expect(view.getByText('High confidence')).toBeInTheDocument();
    expect(article.querySelector('.confidence__icon')).not.toBeNull();
    const photos = view.getAllByRole('img', { name: /^Photo taken/ });
    expect(photos).toHaveLength(2);
    expect(photos[0]).toHaveAttribute('src', '/api/photos/p1-1/thumbnail');
    expect(photos[0]).toHaveAttribute('alt', 'Photo taken Mar 15, 2027 in Orlando');
    expect(view.getByRole('img', { name: 'Photo route with 2 stops' })).toBeInTheDocument();
    expectNoAccessibilityProblems();
  });

  it('has Add trip as the primary button, and Not a trip as a secondary one that is never red', async () => {
    inboxServer([orlando]);
    renderInbox();
    expect(await screen.findByRole('button', { name: 'Add trip' })).toHaveClass('button');
    expect(screen.getByRole('button', { name: 'Add trip' })).not.toHaveClass('button--secondary');
    expect(screen.getByRole('button', { name: 'Not a trip' })).toHaveClass('button--secondary');
    expect(screen.getByRole('button', { name: 'Later' })).toHaveClass('button--text');
  });
});

describe('the inbox list', () => {
  it('lists new suggestions first and later ones last, and the badge counts new ones only', async () => {
    inboxServer([paris, orlando, lakeside]);
    renderInbox();

    await screen.findByRole('heading', { name: sentence(orlando) });
    const titles = screen
      .getAllByRole('heading', { level: 2 })
      .map((heading) => heading.textContent);
    expect(titles).toEqual([sentence(orlando), sentence(lakeside), sentence(paris)]);
    const link = await screen.findByRole('link', { name: 'Inbox, 2 possible trips' });
    expect(within(link).getByText('2')).toHaveClass('nav__badge');
  });

  it('has no Later button on a suggestion that was already set aside', async () => {
    inboxServer([paris]);
    renderInbox();
    const article = (await screen.findByRole('heading', { name: sentence(paris) })).closest(
      'article',
    )!;
    expect(within(article).queryByRole('button', { name: 'Later' })).not.toBeInTheDocument();
    expect(within(article).getByRole('button', { name: 'Add trip' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Inbox' })).toBeInTheDocument();
  });

  it('says No possible trips right now and offers Scan now when there is nothing to review', async () => {
    const server = inboxServer([], {
      'POST /api/scan': status(202, {
        state: 'running',
        done: 0,
        total: null,
        message: null,
        finishedAt: null,
        photoCount: 5200,
      }),
    });
    renderInbox();
    expect(await screen.findByText(/^No possible trips right now\./)).toBeInTheDocument();

    const scan = screen.getByRole('button', { name: 'Scan now' });
    await waitFor(() => expect(scan).toBeEnabled());
    await userEvent.click(scan);
    expect(server.sent('POST', '/api/scan')).toHaveLength(1);
  });

  it('says it is still scanning while the first scan runs', async () => {
    inboxServer([], {
      'GET /api/home': { confirmed: null, suggestion: null, hasPositions: false, photoCount: 0 },
      'GET /api/scan': {
        state: 'running',
        done: 0,
        total: 48000,
        message: null,
        finishedAt: null,
        photoCount: 0,
      },
    });
    renderInbox();
    expect(
      await screen.findByText('No trips yet. Fibotrail is still scanning your library.'),
    ).toBeInTheDocument();
  });
});

describe('the three actions', () => {
  it('Add trip opens a sheet with the suggested name and cover, and Add saves what you chose', async () => {
    const server = inboxServer([orlando, lakeside]);
    renderInbox();
    await userEvent.click((await screen.findAllByRole('button', { name: 'Add trip' }))[0]!);

    const sheet = screen.getByRole('dialog', { name: 'Add this trip' });
    const name = within(sheet).getByLabelText('Name');
    expect(name).toHaveValue('Orlando, March 2027');
    expect(name).toHaveFocus();
    expect(
      await within(sheet).findByRole('button', { name: /^Use as cover: Photo taken Mar 15/ }),
    ).toHaveAttribute('aria-pressed', 'true');
    expectNoAccessibilityProblems();

    await userEvent.clear(name);
    await userEvent.type(name, 'Disney with the kids');
    await userEvent.click(
      within(sheet).getByRole('button', { name: /^Use as cover: Photo taken Mar 18/ }),
    );
    await userEvent.click(within(sheet).getByRole('button', { name: 'Add' }));

    const sent = server.sent('POST', '/api/trips/1/decision')[0]!;
    expect(sent.body).toEqual({
      action: 'add',
      name: 'Disney with the kids',
      coverPhotoId: 'other-1',
    });
    expect(sent.headers['X-CSRF-Token']).toBe('token-1');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: sentence(orlando) })).not.toBeInTheDocument(),
    );
    expect(await screen.findByRole('link', { name: 'Inbox, 1 possible trip' })).toBeInTheDocument();
  });

  it('keeps the sheet open and says why when the name is refused', async () => {
    inboxServer([orlando], {
      'POST /api/trips/1/decision': status(400, {
        detail: 'Give the trip a name of 1 to 100 characters.',
      }),
    });
    renderInbox();
    await userEvent.click(await screen.findByRole('button', { name: 'Add trip' }));
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    const sheet = screen.getByRole('dialog');
    expect(await within(sheet).findByRole('alert')).toHaveTextContent('Give the trip a name');
  });

  it('closes the sheet with Cancel or Escape and changes nothing', async () => {
    const server = inboxServer([orlando]);
    renderInbox();
    const add = await screen.findByRole('button', { name: 'Add trip' });
    await userEvent.click(add);
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(add).toHaveFocus();

    await userEvent.click(add);
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(server.sent('POST', '/api/trips/1/decision')).toHaveLength(0);
  });

  it('Not a trip moves the card to Ignored, the badge drops by one, and focus moves to the next card', async () => {
    const server = inboxServer([orlando, lakeside]);
    renderInbox();
    await screen.findByRole('link', { name: 'Inbox, 2 possible trips' });

    await userEvent.click(screen.getAllByRole('button', { name: 'Not a trip' })[0]!);

    expect(server.sent('POST', '/api/trips/1/decision')[0]!.body).toEqual({ action: 'ignore' });
    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: sentence(orlando) })).not.toBeInTheDocument(),
    );
    expect(screen.getByRole('heading', { name: sentence(lakeside) })).toHaveFocus();
    expect(await screen.findByRole('link', { name: 'Inbox, 1 possible trip' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('tab', { name: 'Ignored' }));
    expect(screen.getByRole('heading', { name: sentence(orlando) })).toBeInTheDocument();
  });

  it('Later keeps the card in the inbox, below the new suggestions, and out of the badge count', async () => {
    inboxServer([orlando, lakeside]);
    renderInbox();
    await screen.findByRole('link', { name: 'Inbox, 2 possible trips' });

    await userEvent.click(screen.getAllByRole('button', { name: 'Later' })[0]!);

    expect(await screen.findByRole('link', { name: 'Inbox, 1 possible trip' })).toBeInTheDocument();
    await waitFor(() =>
      expect(
        screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent),
      ).toEqual([sentence(lakeside), sentence(orlando)]),
    );
  });

  it('shows the plain message when a decision fails', async () => {
    inboxServer([orlando], { 'POST /api/trips/1/decision': status(500) });
    renderInbox();
    await userEvent.click(await screen.findByRole('button', { name: 'Not a trip' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong');
    expect(screen.getByRole('heading', { name: sentence(orlando) })).toBeInTheDocument();
  });
});

describe('the Ignored tab', () => {
  it('lists ignored suggestions with one button, and Restore returns the card to the inbox', async () => {
    const server = inboxServer([{ ...orlando, status: 'ignored' }, lakeside]);
    renderInbox();
    await screen.findByRole('link', { name: 'Inbox, 1 possible trip' });

    await userEvent.click(screen.getByRole('tab', { name: 'Ignored' }));
    expect(screen.getByRole('tab', { name: 'Ignored' })).toHaveAttribute('aria-selected', 'true');
    const article = screen.getByRole('heading', { name: sentence(orlando) }).closest('article')!;
    expect(
      within(article)
        .getAllByRole('button')
        .map((button) => button.textContent),
    ).toEqual(['Restore']);

    await userEvent.click(within(article).getByRole('button', { name: 'Restore' }));

    expect(server.sent('POST', '/api/trips/1/decision')[0]!.body).toEqual({ action: 'restore' });
    expect(
      await screen.findByRole('link', { name: 'Inbox, 2 possible trips' }),
    ).toBeInTheDocument();
    expect(
      await screen.findByText(/A suggestion you mark Not a trip waits here/),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole('tab', { name: 'Suggestions' }));
    expect(screen.getByRole('heading', { name: sentence(orlando) })).toBeInTheDocument();
  });
});

describe('swipe on a touch screen', () => {
  function swipe(element: Element, dx: number, dy = 0, pointerType = 'touch') {
    fireEvent.pointerDown(element, { pointerType, clientX: 200, clientY: 300 });
    fireEvent.pointerMove(element, { pointerType, clientX: 200 + dx, clientY: 300 + dy });
    fireEvent.pointerUp(element, { pointerType, clientX: 200 + dx, clientY: 300 + dy });
  }

  it('a swipe right opens the same sheet as Add trip', async () => {
    inboxServer([orlando]);
    renderInbox();
    const article = (await screen.findByRole('heading', { name: sentence(orlando) })).closest(
      'article',
    )!;
    swipe(article, SWIPE_DISTANCE + 10);
    expect(await screen.findByRole('dialog', { name: 'Add this trip' })).toBeInTheDocument();
  });

  it('a swipe left is Not a trip', async () => {
    const server = inboxServer([orlando]);
    renderInbox();
    const article = (await screen.findByRole('heading', { name: sentence(orlando) })).closest(
      'article',
    )!;
    await screen.findByRole('button', { name: 'Sign out', hidden: true }).catch(() => undefined);
    await waitFor(() => expect(server.sent('GET', '/api/session')).toHaveLength(1));
    await screen.findByRole('link', { name: 'Inbox, 1 possible trip' });
    swipe(article, -SWIPE_DISTANCE - 10);
    await waitFor(() =>
      expect(server.sent('POST', '/api/trips/1/decision')[0]?.body).toEqual({ action: 'ignore' }),
    );
  });

  it('a short swipe, a scroll, and a mouse drag do nothing', async () => {
    const server = inboxServer([orlando]);
    renderInbox();
    const article = (await screen.findByRole('heading', { name: sentence(orlando) })).closest(
      'article',
    )!;
    swipe(article, SWIPE_DISTANCE - 20);
    swipe(article, -120, 300);
    swipe(article, 200, 0, 'mouse');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(server.sent('POST', '/api/trips/1/decision')).toHaveLength(0);
  });
});

describe('with a keyboard only', () => {
  it('you can add one suggestion and ignore another', async () => {
    const server = inboxServer([orlando, lakeside]);
    renderInbox();
    await screen.findByRole('link', { name: 'Inbox, 2 possible trips' });
    const user = userEvent.setup();

    screen.getAllByRole('button', { name: 'Add trip' })[0]!.focus();
    await user.keyboard('{Enter}');
    expect(screen.getByLabelText('Name')).toHaveFocus();
    await user.keyboard('{Enter}');
    await waitFor(() => expect(server.sent('POST', '/api/trips/1/decision')).toHaveLength(1));
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: sentence(lakeside) })).toHaveFocus(),
    );

    await user.tab();
    await user.tab();
    expect(screen.getByRole('button', { name: 'Not a trip' })).toHaveFocus();
    await user.keyboard(' ');
    await waitFor(() =>
      expect(server.sent('POST', '/api/trips/2/decision')[0]?.body).toEqual({ action: 'ignore' }),
    );
  });
});

describe('formatDateRange', () => {
  it.each([
    ['2027-03-15', '2027-03-24', 'Mar 15–24, 2027'],
    ['2027-03-28', '2027-04-03', 'Mar 28–Apr 3, 2027'],
    ['2026-12-28', '2027-01-03', 'Dec 28, 2026–Jan 3, 2027'],
    ['2027-03-15', '2027-03-15', 'Mar 15, 2027'],
  ])('%s to %s is "%s"', (start, end, expected) => {
    expect(formatDateRange(start, end)).toBe(expected);
  });
});

describe('projectRoute', () => {
  it('puts north at the top and east on the right, inside the drawing', () => {
    const [southWest, northEast] = projectRoute([
      { latitude: 28.4, longitude: -81.55 },
      { latitude: 28.5, longitude: -81.4 },
    ]);
    expect(northEast!.y).toBeLessThan(southWest!.y);
    expect(northEast!.x).toBeGreaterThan(southWest!.x);
    for (const point of [southWest!, northEast!]) {
      expect(point.x).toBeGreaterThanOrEqual(16);
      expect(point.x).toBeLessThanOrEqual(144);
      expect(point.y).toBeGreaterThanOrEqual(16);
      expect(point.y).toBeLessThanOrEqual(84);
    }
  });

  it('centers a single stop', () => {
    expect(projectRoute([{ latitude: 10, longitude: 20 }])).toEqual([{ x: 80, y: 50 }]);
  });
});
