import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { App } from '../App';
import { fakeServer } from '../test/server';
import { installMatchMedia, resizeTo } from '../test/window';
import { navigationModeForWidth } from './useNavigationMode';

function renderApp(path = '/') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  fakeServer();
});

describe('navigation by width', () => {
  it.each([
    [320, 'tabs'],
    [599, 'tabs'],
    [600, 'rail'],
    [1023, 'rail'],
    [1024, 'sidebar'],
    [1920, 'sidebar'],
  ])('at %i px the navigation is "%s"', (width, mode) => {
    expect(navigationModeForWidth(width)).toBe(mode);
    installMatchMedia(width);
    const { container } = renderApp();
    expect(container.querySelector('.app')).toHaveAttribute('data-nav', mode);
  });

  it('changes without a reload when the width changes', () => {
    installMatchMedia(599);
    const { container } = renderApp();
    resizeTo(600);
    expect(container.querySelector('.app')).toHaveAttribute('data-nav', 'rail');
    resizeTo(1024);
    expect(container.querySelector('.app')).toHaveAttribute('data-nav', 'sidebar');
  });

  it('keeps a label on each item for screen readers when the rail shows icons only', () => {
    installMatchMedia(700);
    renderApp();
    const nav = screen.getByRole('navigation', { name: 'Main' });
    expect(within(nav).getByRole('link', { name: 'Trips' })).toBeInTheDocument();
    expect(within(nav).getByText('Trips')).toHaveClass('visually-hidden');
  });
});

describe('the app shell', () => {
  beforeEach(() => installMatchMedia(390));

  it('has Trips and Map in the tab bar, and Inbox and Settings as icon buttons in the top bar', () => {
    renderApp();
    const tabs = within(screen.getByRole('navigation', { name: 'Main' })).getAllByRole('link');
    expect(tabs.map((link) => link.textContent)).toEqual(['Trips', 'Map', 'Stats']);
    tabs.forEach((link) => expect(link.querySelector('svg')).not.toBeNull());

    const tools = within(
      screen.getByRole('navigation', { name: 'Inbox and settings' }),
    ).getAllByRole('link');
    expect(tools.map((link) => link.getAttribute('aria-label'))).toEqual(['Inbox', 'Settings']);
    tools.forEach((link) => expect(link.querySelector('svg')).not.toBeNull());
    expect(screen.getByRole('banner')).toContainElement(tools[0]!);
  });

  it('opens on Trips and marks it as the current page', () => {
    renderApp('/');
    expect(screen.getByRole('heading', { level: 1, name: 'Trips' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Trips' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Inbox' })).not.toHaveAttribute('aria-current');
  });

  it('moves between screens and names each one in the header and the page title', async () => {
    renderApp();
    await userEvent.click(screen.getByRole('link', { name: 'Inbox' }));
    expect(screen.getByRole('heading', { level: 1, name: 'Inbox' })).toBeInTheDocument();
    expect(document.title).toBe('Inbox · Fibotrail');
    await userEvent.click(screen.getByRole('link', { name: 'Settings' }));
    expect(screen.getByRole('link', { name: 'Settings' })).toHaveAttribute('aria-current', 'page');
  });

  it('sends an unknown address to Trips', () => {
    renderApp('/nowhere');
    expect(screen.getByRole('heading', { level: 1, name: 'Trips' })).toBeInTheDocument();
  });

  it('signs out with a form post that carries the anti-forgery token', async () => {
    renderApp('/settings');
    const button = await screen.findByRole('button', { name: 'Sign out' });
    const form = button.closest('form')!;
    expect(form).toHaveAttribute('method', 'post');
    expect(form).toHaveAttribute('action', '/sign-out');
    expect(form.querySelector('input[name="csrf"]')).toHaveValue('token-1');
  });
});

describe('the Inbox and Settings by width', () => {
  it.each([
    [600, 'rail'],
    [1024, 'sidebar'],
  ])(
    'at %i px the sidebar holds all four destinations and the top bar has no tools',
    (width, mode) => {
      installMatchMedia(width);
      const { container } = renderApp();

      expect(container.querySelector('.app')).toHaveAttribute('data-nav', mode);
      const items = within(screen.getByRole('navigation', { name: 'Main' })).getAllByRole('link');
      expect(items.map((link) => link.textContent)).toEqual([
        'Trips',
        'Map',
        'Stats',
        'Inbox',
        'Settings',
      ]);
      expect(
        screen.queryByRole('navigation', { name: 'Inbox and settings' }),
      ).not.toBeInTheDocument();
    },
  );

  it('carries the Inbox badge to whichever place the Inbox is in', async () => {
    fakeServer({ 'GET /api/inbox/count': { count: 3 } });
    installMatchMedia(390);
    const { unmount } = renderApp();
    const topBar = await screen.findByRole('link', { name: 'Inbox, 3 possible trips' });
    expect(screen.getByRole('navigation', { name: 'Inbox and settings' })).toContainElement(topBar);
    expect(within(topBar).getByText('3')).toHaveClass('nav__badge');
    unmount();

    installMatchMedia(1280);
    renderApp();
    const sidebar = await screen.findByRole('link', { name: 'Inbox, 3 possible trips' });
    expect(screen.getByRole('navigation', { name: 'Main' })).toContainElement(sidebar);
  });
});
