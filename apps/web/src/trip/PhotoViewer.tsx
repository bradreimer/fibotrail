import { useEffect, useMemo, useRef, useState, type KeyboardEvent, type PointerEvent } from 'react';

import { LazyMap } from '../map/LazyMap';
import type { MapMarker } from '../map/MapView';
import { formatPhotoTime } from './format';
import type { TripPhoto } from './PhotoTile';

/** How far a swipe must travel, in pixels, to count. */
export const VIEWER_SWIPE = 64;

interface PhotoViewerProps {
  photos: readonly TripPhoto[];
  index: number;
  onIndexChange: (index: number) => void;
  onClose: () => void;
}

/**
 * One photo, full screen. A swipe or an arrow key moves through the trip's photos. A swipe down,
 * Close, or Escape returns to the page.
 */
export function PhotoViewer({ photos, index, onIndexChange, onClose }: PhotoViewerProps) {
  const photo = photos[index]!;
  const close = useRef<HTMLButtonElement>(null);
  const start = useRef<{ x: number; y: number } | null>(null);
  const [offset, setOffset] = useState({ x: 0, y: 0 });

  // Focus starts on Close, and returns to the photo that was opened.
  useEffect(() => {
    const before = document.activeElement as HTMLElement | null;
    close.current?.focus();
    return () => before?.focus?.();
  }, []);

  const go = (step: number) => {
    const next = index + step;
    if (next >= 0 && next < photos.length) onIndexChange(next);
  };

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape') onClose();
    else if (event.key === 'ArrowRight') go(1);
    else if (event.key === 'ArrowLeft') go(-1);
    else if (event.key === 'Tab') {
      // Tab stays inside the viewer while it's open.
      const controls = [
        ...event.currentTarget.querySelectorAll<HTMLElement>('button:not([disabled]), a[href]'),
      ];
      const first = controls[0];
      const last = controls[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    }
  }

  function onPointerDown(event: PointerEvent) {
    if (event.pointerType === 'touch') start.current = { x: event.clientX, y: event.clientY };
  }

  function onPointerMove(event: PointerEvent) {
    if (start.current)
      setOffset({ x: event.clientX - start.current.x, y: event.clientY - start.current.y });
  }

  function onPointerEnd() {
    if (!start.current) return;
    start.current = null;
    const { x, y } = offset;
    setOffset({ x: 0, y: 0 });
    if (y > VIEWER_SWIPE && y > Math.abs(x)) onClose();
    else if (x <= -VIEWER_SWIPE) go(1);
    else if (x >= VIEWER_SWIPE) go(-1);
  }

  const markers = useMemo<MapMarker[]>(
    () =>
      photo.latitude !== null && photo.longitude !== null
        ? [
            {
              id: photo.id,
              kind: 'start',
              label: `Taken here${photo.place ? `: ${photo.place}` : ''}`,
              latitude: photo.latitude,
              longitude: photo.longitude,
            },
          ]
        : [],
    [photo],
  );

  const moving = offset.x !== 0 || offset.y !== 0;
  return (
    // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- the keys work anywhere in the dialog.
    <div
      className="viewer"
      role="dialog"
      aria-modal="true"
      aria-label={`Photo ${index + 1} of ${photos.length}`}
      onKeyDown={onKeyDown}
    >
      <div className="viewer__bar">
        <button type="button" className="viewer__button" ref={close} onClick={onClose}>
          Close
        </button>
        <span className="viewer__count" aria-live="polite">
          {index + 1} of {photos.length}
        </span>
        <span className="viewer__nav">
          <button
            type="button"
            className="viewer__button"
            disabled={index === 0}
            onClick={() => go(-1)}
          >
            Previous
          </button>
          <button
            type="button"
            className="viewer__button"
            disabled={index === photos.length - 1}
            onClick={() => go(1)}
          >
            Next
          </button>
        </span>
      </div>
      <div
        className="viewer__stage"
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerEnd}
        onPointerCancel={onPointerEnd}
      >
        <img
          key={photo.id}
          className="viewer__image"
          src={`/api/photos/${photo.id}/preview`}
          alt={photo.alt}
          draggable={false}
          style={
            moving
              ? { transform: `translate(${offset.x}px, ${Math.max(0, offset.y)}px)` }
              : undefined
          }
        />
      </div>
      <div className="viewer__details">
        <p className="viewer__time">{formatPhotoTime(photo.localTime)}</p>
        {photo.place && <p>{photo.place}</p>}
        {markers.length > 0 && (
          <LazyMap
            className="viewer__map"
            label={`Map of where this photo was taken${photo.place ? `, ${photo.place}` : ''}`}
            markers={markers}
            interactive={false}
          />
        )}
        {photo.immichUrl && (
          <a
            className="button button--secondary"
            href={photo.immichUrl}
            target="_blank"
            rel="noopener noreferrer"
          >
            {photo.isVideo ? 'Play in Immich' : 'Open in Immich'}
          </a>
        )}
      </div>
    </div>
  );
}
