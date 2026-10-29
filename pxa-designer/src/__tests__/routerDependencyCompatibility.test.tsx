/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { Link, MemoryRouter, NavLink, Navigate, Outlet, Route, Routes, useLocation, useNavigate } from 'react-router-dom';

const actEnvironment = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
actEnvironment.IS_REACT_ACT_ENVIRONMENT = true;

function Layout() {
  return <>
    <NavLink to="template">Templates</NavLink>
    <NavLink to="create?mode=code">Code</NavLink>
    <Outlet />
  </>;
}

function LocationProbe() {
  const location = useLocation();
  const navigate = useNavigate();
  return <>
    <output>{location.pathname}{location.search}{location.hash}</output>
    <button onClick={() => navigate(-1)}>Back</button>
    <button onClick={() => navigate(1)}>Forward</button>
    <Link to="/spreadsheet/import">Spreadsheet</Link>
  </>;
}

describe('actual Router dependency compatibility', () => {
  let container: HTMLDivElement;
  let root: Root;
  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
  });
  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  const render = async (entry: string) => {
    await act(async () => root.render(
      <MemoryRouter initialEntries={[entry]}>
        <Routes>
          <Route path="/pdf" element={<Layout />}>
            <Route index element={<Navigate to="template" replace />} />
            <Route path="template" element={<LocationProbe />} />
            <Route path="create" element={<LocationProbe />} />
          </Route>
          <Route path="/spreadsheet/import" element={<LocationProbe />} />
          <Route path="/legacy" element={<Navigate to="/pdf/create?mode=code" replace />} />
        </Routes>
      </MemoryRouter>,
    ));
  };
  const click = async (selector: string) => {
    const target = container.querySelector<HTMLElement>(selector);
    expect(target).not.toBeNull();
    await act(async () => target!.click());
  };
  const location = () => container.querySelector('output')?.textContent;

  test('nested index redirect and relative links retain the PDF parent and active state', async () => {
    await render('/pdf');
    expect(location()).toBe('/pdf/template');
    expect(container.querySelector('[aria-current="page"]')?.textContent).toBe('Templates');
    await click('a[href="/pdf/create?mode=code"]');
    expect(location()).toBe('/pdf/create?mode=code');
    expect(container.querySelector('[aria-current="page"]')?.textContent).toBe('Code');
  });

  test('back and forward preserve query, fragment and domain-specific routes', async () => {
    await render('/pdf/create?mode=code#source');
    await click('a[href="/spreadsheet/import"]');
    expect(location()).toBe('/spreadsheet/import');
    await click('button:nth-of-type(1)');
    expect(location()).toBe('/pdf/create?mode=code#source');
    await click('button:nth-of-type(2)');
    expect(location()).toBe('/spreadsheet/import');
  });

  test('legacy replace redirect retains code mode and remains navigable', async () => {
    await render('/legacy');
    expect(location()).toBe('/pdf/create?mode=code');
    await click('a[href="/pdf/template"]');
    expect(location()).toBe('/pdf/template');
    await click('button:nth-of-type(1)');
    expect(location()).toBe('/pdf/create?mode=code');
  });
});
