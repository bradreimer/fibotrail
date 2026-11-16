import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { fakeServer } from '../../../test/server';
import ThemeParks from '.';

const groups = [
  {
    collectionId: 'universal-parks',
    name: 'Universal parks',
    totalDays: 6,
    visited: 1,
    parks: [
      {
        id: 'usf',
        name: 'Universal Studios Florida',
        group: 'UO',
        days: 6,
        trips: 2,
        firstVisit: '2024-03-10',
      },
      { id: 'ioa', name: 'Islands of Adventure', group: 'UO', days: 0, trips: 0, firstVisit: null },
    ],
  },
];

function renderView() {
  return render(
    <MemoryRouter>
      <ThemeParks />
    </MemoryRouter>,
  );
}

describe('Theme parks', () => {
  it('reads each park as days and trips, and says when a park is not visited', async () => {
    fakeServer({ 'GET /api/stats/theme-parks': groups });
    renderView();

    expect(await screen.findByText('6 days · 2 trips')).toBeInTheDocument();
    expect(screen.getByText('0 days · Not visited yet')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Universal parks' })).toHaveAttribute(
      'href',
      '/collections/universal-parks',
    );
  });

  it('says so with no collection files, without an error', async () => {
    fakeServer({ 'GET /api/stats/theme-parks': [] });
    renderView();

    expect(await screen.findByText(/No collections yet/)).toBeInTheDocument();
  });
});
