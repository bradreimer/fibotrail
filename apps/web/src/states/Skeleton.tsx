interface SkeletonProps {
  /** "card" is a 4:3 block, and "text" is one line. */
  shape?: 'card' | 'text';
  count?: number;
  /** What is loading, for screen readers. */
  label?: string;
}

/** The loading state: gray shapes in the final layout, never a blank page. */
export function Skeleton({ shape = 'text', count = 3, label = 'Loading' }: SkeletonProps) {
  return (
    <div className="skeleton-list" role="status" aria-busy="true">
      <span className="visually-hidden">{label}</span>
      {Array.from({ length: count }, (_, index) => (
        <span key={index} className={`skeleton skeleton--${shape}`} aria-hidden="true" />
      ))}
    </div>
  );
}
