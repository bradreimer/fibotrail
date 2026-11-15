import { lazy, Suspense } from 'react';

import type { MapViewProps } from './MapView';

const MapView = lazy(() => import('./MapView'));

/** Shows a map. The map code loads on first use, behind a skeleton in the final size. */
export function LazyMap(props: MapViewProps & { className?: string }) {
  const { className, ...map } = props;
  return (
    <div className={`map-frame${className ? ` ${className}` : ''}`}>
      <Suspense fallback={<span className="skeleton map-frame__loading" aria-hidden="true" />}>
        <MapView {...map} />
      </Suspense>
    </div>
  );
}
