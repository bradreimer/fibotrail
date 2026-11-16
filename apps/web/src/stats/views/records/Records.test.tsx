import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { fakeServer } from '../../../test/server';
import Records from '.';

const ref = (id: number, name: string) => ({
  id,
  name,
  startDate: '2024-01-01',
  endDate: '2024-01-09',
  days: 9,
});

describe('Records', () => {
  it('names each record and links to its trip or country', async () => {
    fakeServer({
      'GET /api/stats/records': {
        longestTrip: { trip: ref(2, 'Japan'), value: 9 },
        farthestTrip: { trip: ref(2, 'Japan'), value: 9500 },
        mostCountriesTrip: { trip: ref(3, 'Interrail'), value: 4 },
        mostVisitedCountry: { countryId: '752', name: 'Sweden', trips: 3, days: 19 },
        longestGap: { days: 120, after: ref(1, 'Oslo'), before: ref(2, 'Japan') },
      },
    });
    render(
      <MemoryRouter>
        <Records />
      </MemoryRouter>,
    );

    expect(await screen.findByText('Longest trip')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sweden' })).toHaveAttribute(
      'href',
      '/stats/countries/752',
    );
    expect(screen.getByRole('link', { name: 'Interrail' })).toHaveAttribute('href', '/trips/3');
    expect(screen.getByText(/120 days, from/)).toBeInTheDocument();
  });

  it('says so with no trips', async () => {
    fakeServer({
      'GET /api/stats/records': {
        longestTrip: null,
        farthestTrip: null,
        mostCountriesTrip: null,
        mostVisitedCountry: null,
        longestGap: null,
      },
    });
    render(
      <MemoryRouter>
        <Records />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/No trips yet/)).toBeInTheDocument();
  });
});
