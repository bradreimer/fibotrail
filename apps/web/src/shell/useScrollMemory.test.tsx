import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRef } from 'react';
import { Link, MemoryRouter, Route, Routes, useNavigate } from 'react-router-dom';
import { expect, it } from 'vitest';

import { useScrollMemory } from './useScrollMemory';

function Frame() {
  const area = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();
  useScrollMemory(area);
  return (
    <div>
      <div ref={area} data-testid="area">
        <Routes>
          <Route path="/list" element={<Link to="/detail">Open</Link>} />
          <Route path="/detail" element={<p>Detail</p>} />
        </Routes>
      </div>
      <button onClick={() => navigate(-1)}>Back</button>
    </div>
  );
}

it('returns to the scroll position you left when you go back, and starts a new screen at the top', async () => {
  render(
    <MemoryRouter initialEntries={['/list']}>
      <Frame />
    </MemoryRouter>,
  );
  const area = screen.getByTestId('area');

  act(() => {
    area.scrollTop = 840;
    area.dispatchEvent(new Event('scroll'));
  });
  await userEvent.click(screen.getByRole('link', { name: 'Open' }));
  expect(area.scrollTop).toBe(0);

  await userEvent.click(screen.getByRole('button', { name: 'Back' }));
  expect(area.scrollTop).toBe(840);
});
