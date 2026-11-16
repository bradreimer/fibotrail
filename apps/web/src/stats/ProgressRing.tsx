/** A ring that fills clockwise from the top by `visited / total`, with the count in its middle. */
export function ProgressRing({ visited, total }: { visited: number; total: number }) {
  const radius = 20;
  const circumference = 2 * Math.PI * radius;
  const share = total > 0 ? Math.min(visited / total, 1) : 0;
  return (
    <svg className="ring" viewBox="0 0 48 48" aria-hidden="true" focusable="false">
      <circle className="ring__track" cx="24" cy="24" r={radius} fill="none" strokeWidth="5" />
      {visited > 0 && (
        <circle
          className="ring__fill"
          cx="24"
          cy="24"
          r={radius}
          fill="none"
          strokeWidth="5"
          strokeLinecap="round"
          strokeDasharray={`${share * circumference} ${circumference}`}
          transform="rotate(-90 24 24)"
        />
      )}
      <text className="ring__text" x="24" y="24" textAnchor="middle" dominantBaseline="central">
        {visited}
      </text>
    </svg>
  );
}
