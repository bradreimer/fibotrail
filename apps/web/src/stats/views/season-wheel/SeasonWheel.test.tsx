import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { fakeServer, status } from '../../../test/server';
import SeasonWheel from '.';

const monthsOf = (days: Record<number, number>) =>
  Array.from({ length: 12 }, (_, index) => ({
    month: index + 1,
    days: days[index + 1] ?? 0,
    hemisphere: 'north',
  }));

const sweden = {
  countryName: 'Sweden',
  hemisphere: 'north',
  arcHemisphere: 'north',
  seasonsSeen: 2,
  months: monthsOf({ 1: 2, 7: 5, 8: 7, 12: 5 }),
  seasons: [
    { name: 'Winter', months: [12, 1, 2], days: 7, seen: true },
    { name: 'Spring', months: [3, 4, 5], days: 0, seen: false },
    { name: 'Summer', months: [6, 7, 8], days: 12, seen: true },
    { name: 'Autumn', months: [9, 10, 11], days: 0, seen: false },
  ],
};

const path = '/api/stats/countries/752/season-wheel';

describe('the season wheel', () => {
  it('reads the seasons seen, with a text alternative that lists each season', async () => {
    fakeServer({ [`GET ${path}`]: sweden });
    render(<SeasonWheel countryId="752" />);

    const text = (await screen.findByText(/^Sweden, 2 of 4 seasons seen\./)).textContent!;

    expect(text).toBe(
      'Sweden, 2 of 4 seasons seen. Winter: 7 days. Spring: not yet. Summer: 12 days. Autumn: not yet.',
    );
    expect(screen.getByText('Northern hemisphere')).toBeInTheDocument();
  });

  it('has a row for each season with its months and days, or Not yet', async () => {
    fakeServer({ [`GET ${path}`]: sweden });
    render(<SeasonWheel countryId="752" />);

    const rows = await screen.findAllByRole('button');

    expect(rows.map((row) => row.textContent)).toEqual([
      '✓WinterDec–Feb7 days',
      'SpringMar–MayNot yet',
      '✓SummerJun–Aug12 days',
      'AutumnSep–NovNot yet',
    ]);
  });

  it('dims the months outside a tapped season, shows its days in the center, and clears on a second tap', async () => {
    fakeServer({ [`GET ${path}`]: sweden });
    const { container } = render(<SeasonWheel countryId="752" />);
    const summer = (await screen.findAllByRole('button'))[2]!;

    await userEvent.click(summer);

    expect(summer).toHaveAttribute('aria-pressed', 'true');
    expect(container.querySelectorAll('.season__dim')).toHaveLength(9 + 3);
    expect(container.querySelector('.season__big')!.textContent).toBe('12');

    await userEvent.click(summer);
    expect(container.querySelectorAll('.season__dim')).toHaveLength(0);
    expect(container.querySelector('.season__big')!.textContent).toBe('2 of 4');
  });

  it('says every season when all four are seen', async () => {
    fakeServer({
      [`GET ${path}`]: {
        ...sweden,
        seasonsSeen: 4,
        seasons: sweden.seasons.map((season) => ({ ...season, days: 1, seen: true })),
      },
    });
    const { container } = render(<SeasonWheel countryId="752" />);
    await screen.findAllByRole('button');

    expect(container.querySelector('.season__caption')!.textContent).toBe('every season');
  });

  it('names both hemispheres for a country that spans the equator', async () => {
    fakeServer({ [`GET ${path}`]: { ...sweden, hemisphere: 'both' } });
    render(<SeasonWheel countryId="752" />);

    expect(await screen.findByText('Both hemispheres')).toBeInTheDocument();
  });

  it('shows an error with a way to try again', async () => {
    fakeServer({ [`GET ${path}`]: status(500, {}) });
    render(<SeasonWheel countryId="752" />);

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});
