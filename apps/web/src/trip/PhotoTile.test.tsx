import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { DEFAULT_RATIO, PhotoTile, type TripPhoto } from './PhotoTile';

const photo: TripPhoto = {
  id: 'p1',
  alt: 'Photo taken 2027-03-15 in Orlando',
  localTime: '2027-03-15T09:00:00',
  latitude: null,
  longitude: null,
  place: null,
  stopIndex: null,
  isVideo: false,
  durationMs: null,
  immichUrl: null,
  onTheWay: false,
};

function load(image: HTMLElement, width: number, height: number) {
  Object.defineProperty(image, 'naturalWidth', { value: width, configurable: true });
  Object.defineProperty(image, 'naturalHeight', { value: height, configurable: true });
  fireEvent.load(image);
}

describe('PhotoTile', () => {
  it('guesses 4:3 until the thumbnail loads', () => {
    render(<PhotoTile photo={photo} onOpen={vi.fn()} />);

    expect(DEFAULT_RATIO).toBeCloseTo(4 / 3);
    expect(screen.getByRole('button')).toHaveStyle({ aspectRatio: String(DEFAULT_RATIO) });
  });

  it('takes the proportions of the photo once the thumbnail has loaded', () => {
    const { container } = render(<PhotoTile photo={photo} onOpen={vi.fn()} />);
    const image = container.querySelector('img')!;

    load(image, 300, 400);
    expect(screen.getByRole('button')).toHaveStyle({ aspectRatio: String(300 / 400) });

    load(image, 400, 300);
    expect(screen.getByRole('button')).toHaveStyle({ aspectRatio: String(400 / 300) });
  });

  it('keeps the guess when the thumbnail reports no size', () => {
    const { container } = render(<PhotoTile photo={photo} onOpen={vi.fn()} />);

    load(container.querySelector('img')!, 0, 0);

    expect(screen.getByRole('button')).toHaveStyle({ aspectRatio: String(DEFAULT_RATIO) });
  });

  it('shows a small mark on a photo that is on the way, and none on any other', () => {
    const { container, rerender } = render(<PhotoTile photo={photo} onOpen={vi.fn()} />);
    expect(container.querySelector('.photo-tile__hidden')).toBeNull();

    rerender(
      <PhotoTile
        photo={{ ...photo, onTheWay: true, alt: `${photo.alt}, hidden from the map` }}
        onOpen={vi.fn()}
      />,
    );

    expect(container.querySelector('.photo-tile__hidden')).not.toBeNull();
    // The mark is for the eye. A screen reader gets the alt text, which says so.
    expect(screen.getByRole('button', { name: /hidden from the map$/ })).toBeInTheDocument();
    expect(container.querySelector('.photo-tile__hidden')).toHaveAttribute('aria-hidden', 'true');
  });
});
