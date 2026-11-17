import type { components } from '../api/schema';
import { formatDateRange } from '../trips/dates';
import { CollapsibleSection } from './CollapsibleSection';

export type MapDetailCountry = components['schemas']['MapDetailCountry'];
export type MapRegion = components['schemas']['MapRegion'];

const number = new Intl.NumberFormat('en-US');

/** "7 of 13 provinces and territories": how many of a detail country's regions you've visited. */
export function progressText(country: MapDetailCountry): string {
  return `${number.format(country.regions.length)} of ${number.format(country.totalRegions)} ${country.regionNoun}`;
}

/** The anchor and key of a detail country's region list. */
export function regionSectionId(country: MapDetailCountry): string {
  return `regions-${country.id}`;
}

/**
 * One line under the numbers for each detail country, such as Canada's provinces and territories.
 * The country's name links to its region list below the map.
 */
export function RegionProgress({
  countries,
  onJump,
}: {
  countries: readonly MapDetailCountry[];
  onJump: (sectionId: string) => void;
}) {
  if (countries.length === 0) return null;
  return (
    <section aria-labelledby="region-progress">
      <h3 className="visually-hidden" id="region-progress">
        Regions you've visited
      </h3>
      <ul className="region-progress">
        {countries.map((country) => (
          <li key={country.id} className="region-progress__item">
            <a
              className="region-progress__country"
              href={`#${regionSectionId(country)}`}
              onClick={(event) => {
                event.preventDefault();
                onJump(regionSectionId(country));
              }}
            >
              {country.name}
            </a>
            <span className="region-progress__count">{progressText(country)}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}

/**
 * The regions you've visited in each detail country, with the date of the first visit, oldest first.
 * It is also the text alternative to the region fills on the map, and a way to open a small region
 * that is hard to tap.
 */
export function RegionLists({
  countries,
  isOpen,
  onToggle,
  onSelect,
}: {
  countries: readonly MapDetailCountry[];
  isOpen: (sectionId: string) => boolean;
  onToggle: (sectionId: string, open: boolean) => void;
  onSelect: (region: MapRegion) => void;
}) {
  return (
    <>
      {countries.map((country) => (
        <CollapsibleSection
          key={country.id}
          id={regionSectionId(country)}
          title={`${country.name}: ${progressText(country)}`}
          open={isOpen(regionSectionId(country))}
          onToggle={(open) => onToggle(regionSectionId(country), open)}
        >
          {country.regions.length === 0 ? (
            <p className="life-map__note">{`No ${country.regionNoun} yet.`}</p>
          ) : (
            <ol className="first-visits">
              {country.regions.map((region) => (
                <li key={region.id}>
                  <button
                    type="button"
                    className="first-visits__item"
                    onClick={() => onSelect(region)}
                  >
                    <span className="first-visits__name">{region.name}</span>
                    <span className="first-visits__date">
                      {formatDateRange(region.firstVisit, region.firstVisit)}
                    </span>
                  </button>
                </li>
              ))}
            </ol>
          )}
        </CollapsibleSection>
      ))}
    </>
  );
}
