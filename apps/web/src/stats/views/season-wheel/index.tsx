import { useState } from 'react';

import { useApi } from '../../../api/useApi';
import { ErrorState } from '../../../states/ErrorState';
import type { ViewProps } from '../../registry';
import { arcPath, CENTER, labelPoint, monthRange, shade, slicePath } from './geometry';

const INITIALS = ['J', 'F', 'M', 'A', 'M', 'J', 'J', 'A', 'S', 'O', 'N', 'D'];

const HEMISPHERES: Record<string, string> = {
  north: 'Northern hemisphere',
  south: 'Southern hemisphere',
  both: 'Both hemispheres',
};

/** The months and seasons you have spent in a country. See docs/specs/season-wheel.md. */
export default function SeasonWheel({ countryId = '' }: ViewProps) {
  const wheel = useApi(
    '/api/stats/countries/{id}/season-wheel',
    `/api/stats/countries/${encodeURIComponent(countryId)}/season-wheel`,
  );
  const [highlight, setHighlight] = useState<number | null>(null);

  if (wheel.loading && !wheel.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the season wheel</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (wheel.error) return <ErrorState message={wheel.error} onRetry={wheel.reload} />;
  const data = wheel.data;
  if (!data) return null;

  const max = Math.max(...data.months.map((month) => month.days), 0);
  const active = highlight === null ? null : data.seasons[highlight]!;
  const inActive = (month: number) => active === null || active.months.includes(month);
  const centerValue = active
    ? { big: String(active.days), caption: active.days === 1 ? 'day' : 'days' }
    : data.seasonsSeen === 4
      ? { big: '4 of 4', caption: 'every season' }
      : { big: `${data.seasonsSeen} of 4`, caption: 'seasons seen' };

  return (
    <div className="season">
      <p className="stats-page__lead">{HEMISPHERES[data.hemisphere]}</p>
      <svg className="season__wheel" viewBox="0 0 200 200" aria-hidden="true" focusable="false">
        {data.months.map((month) => {
          const label = labelPoint(month.month);
          const level = shade(month.days, max);
          return (
            <g key={month.month} className={inActive(month.month) ? undefined : 'season__dim'}>
              <path className="season__slice" d={slicePath(month.month)} />
              {level > 0 && (
                <path
                  className="season__fill"
                  d={slicePath(month.month)}
                  style={{ fillOpacity: level }}
                />
              )}
              <text
                className={level > 0.6 ? 'season__initial season__initial--on' : 'season__initial'}
                x={label.x}
                y={label.y}
                textAnchor="middle"
                dominantBaseline="central"
              >
                {INITIALS[month.month - 1]}
              </text>
            </g>
          );
        })}
        {data.seasons.map((season, index) => (
          <path
            key={season.name}
            className={`season__arc${season.seen ? ' season__arc--seen' : ''}${
              highlight !== null && highlight !== index ? ' season__dim' : ''
            }`}
            d={arcPath(season.months)}
          />
        ))}
        <text className="season__big" x={CENTER} y={CENTER - 2} textAnchor="middle">
          {centerValue.big}
        </text>
        <text className="season__caption" x={CENTER} y={CENTER + 12} textAnchor="middle">
          {centerValue.caption}
        </text>
      </svg>
      <p className="stats-page__note">
        <span aria-hidden="true">0 days</span>
        <span className="visually-hidden">Darker months have more days. </span>
        <span aria-hidden="true"> to </span>
        <span aria-hidden="true">{max} days</span>
      </p>
      <p className="visually-hidden">
        {`${data.countryName}, ${data.seasonsSeen} of 4 seasons seen. `}
        {data.seasons
          .map(
            (season) =>
              `${season.name}: ${season.seen ? `${season.days} ${season.days === 1 ? 'day' : 'days'}` : 'not yet'}.`,
          )
          .join(' ')}
      </p>
      <ul className="season__rows" aria-label="Seasons">
        {data.seasons.map((season, index) => (
          <li key={season.name}>
            <button
              type="button"
              className="season__row"
              aria-pressed={highlight === index}
              onClick={() => setHighlight(highlight === index ? null : index)}
            >
              <span className="season__check" aria-hidden="true">
                {season.seen ? '✓' : ''}
              </span>
              <span className="season__name">{season.name}</span>
              <span className="stats-list__meta">{monthRange(season.months)}</span>
              <span className="season__days">
                {season.seen ? `${season.days} ${season.days === 1 ? 'day' : 'days'}` : 'Not yet'}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
