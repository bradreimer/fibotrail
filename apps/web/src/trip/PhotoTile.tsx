import { useState } from 'react';

import type { components } from '../api/schema';
import { formatDuration } from './format';

export type TripPhoto = components['schemas']['TripPhotoInfo'];

/** The shape a tile has before its thumbnail loads. */
export const DEFAULT_RATIO = 4 / 3;

/** A photo in the timeline. A video shows its still frame, a play mark, and its length. */
export function PhotoTile({ photo, onOpen }: { photo: TripPhoto; onOpen: () => void }) {
  // The tile is 110 px high and takes its width from the photo's proportions. The server doesn't
  // store photo sizes, so the guess is 4:3 until the thumbnail loads and tells the real shape.
  const [ratio, setRatio] = useState(DEFAULT_RATIO);
  return (
    <button
      type="button"
      className="photo-tile"
      style={{ aspectRatio: ratio }}
      onClick={onOpen}
      aria-label={
        photo.isVideo && photo.durationMs
          ? `${photo.alt}, ${formatDuration(photo.durationMs)}`
          : photo.alt
      }
    >
      <img
        className="photo-tile__image"
        src={`/api/photos/${photo.id}/thumbnail`}
        alt=""
        loading="lazy"
        decoding="async"
        draggable={false}
        onLoad={(event) => {
          const { naturalWidth, naturalHeight } = event.currentTarget;
          if (naturalWidth > 0 && naturalHeight > 0) setRatio(naturalWidth / naturalHeight);
        }}
      />
      {photo.isVideo && (
        <span className="photo-tile__video" aria-hidden="true">
          <svg viewBox="0 0 16 16" className="photo-tile__play" focusable="false">
            <path d="M5 3.5v9l7.5-4.5Z" fill="currentColor" />
          </svg>
          {photo.durationMs ? formatDuration(photo.durationMs) : null}
        </span>
      )}
    </button>
  );
}
