import type { components } from '../api/schema';
import { formatDuration } from './format';

export type TripPhoto = components['schemas']['TripPhotoInfo'];

/** A photo in the timeline. A video shows its still frame, a play mark, and its length. */
export function PhotoTile({ photo, onOpen }: { photo: TripPhoto; onOpen: () => void }) {
  return (
    <button
      type="button"
      className="photo-tile"
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
