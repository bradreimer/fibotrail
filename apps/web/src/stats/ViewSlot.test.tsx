import { render, screen } from '@testing-library/react';
import { lazy } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { fakeServer } from '../test/server';
import type { StatsView } from './registry';
import { ViewSlot } from './ViewSlot';

const hello: StatsView = {
  id: 'hello',
  slot: 'stats',
  title: 'Hello view',
  component: lazy(async () => ({ default: () => <p>hello from a view</p> })),
};

const broken: StatsView = {
  id: 'broken',
  slot: 'stats',
  title: 'Broken view',
  component: lazy(async () => ({
    default: () => {
      throw new Error('boom');
    },
  })),
};

const forCountry: StatsView = {
  id: 'country',
  slot: 'country',
  title: 'Country view',
  component: lazy(async () => ({
    default: ({ countryId }: { countryId?: string }) => <p>for {countryId}</p>,
  })),
};

describe('the view slot', () => {
  it('renders nothing when no view is registered', () => {
    fakeServer();
    const { container } = render(<ViewSlot slot="stats" views={[]} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('renders only the views of its slot, under their headings, and passes the country', async () => {
    fakeServer();
    render(
      <MemoryRouter>
        <ViewSlot slot="stats" views={[hello, forCountry]} />
        <ViewSlot slot="country" countryId="752" views={[hello, forCountry]} />
      </MemoryRouter>,
    );

    expect(await screen.findByText('hello from a view')).toBeInTheDocument();
    expect(await screen.findByText('for 752')).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 3 }).map((h) => h.textContent)).toEqual([
      'Hello view',
      'Country view',
    ]);
  });

  it('keeps one failing view from affecting the others', async () => {
    fakeServer();
    const quiet = console.error;
    console.error = () => {};
    try {
      render(
        <MemoryRouter>
          <ViewSlot slot="stats" views={[broken, hello]} />
        </MemoryRouter>,
      );

      expect(
        await screen.findByText("Broken view couldn't load. Reload the page."),
      ).toBeInTheDocument();
      expect(await screen.findByText('hello from a view')).toBeInTheDocument();
    } finally {
      console.error = quiet;
    }
  });
});
