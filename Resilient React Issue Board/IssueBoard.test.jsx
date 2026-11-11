// @vitest-environment jsdom
import React, { StrictMode } from 'react';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import '@testing-library/jest-dom/vitest';
import { afterEach, describe, expect, it, vi } from 'vitest';
import IssueBoard from './IssueBoard.jsx';

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe('IssueBoard', () => {
  it('debounces search by 300 ms and applies the status filter immediately', async () => {
    vi.useFakeTimers();
    const loadIssues = vi.fn().mockResolvedValue([]);
    render(<IssueBoard loadIssues={loadIssues} />);
    await act(async () => {});
    expect(loadIssues).toHaveBeenCalledTimes(1);

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search issues' }), {
      target: { value: 'rea' },
    });
    await act(async () => vi.advanceTimersByTime(200));
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'react' } });
    await act(async () => vi.advanceTimersByTime(299));
    expect(loadIssues).toHaveBeenCalledTimes(1);
    await act(async () => vi.advanceTimersByTime(1));
    expect(loadIssues).toHaveBeenLastCalledWith({
      query: 'react', status: 'all', signal: expect.any(AbortSignal),
    });

    fireEvent.change(screen.getByRole('combobox', { name: 'Status' }), {
      target: { value: 'open' },
    });
    await act(async () => {});
    expect(loadIssues).toHaveBeenCalledTimes(3);
    expect(loadIssues).toHaveBeenLastCalledWith({
      query: 'react', status: 'open', signal: expect.any(AbortSignal),
    });
  });

  it('aborts stale requests and ignores late successes and failures even when cancellation is ignored', async () => {
    const first = deferred();
    const second = deferred();
    const latest = deferred();
    const loadIssues = vi.fn()
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
      .mockReturnValueOnce(latest.promise);
    render(<IssueBoard loadIssues={loadIssues} />);
    const firstSignal = loadIssues.mock.calls[0][0].signal;
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'open' } });
    const secondSignal = loadIssues.mock.calls[1][0].signal;
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'closed' } });
    expect(firstSignal.aborted).toBe(true);
    expect(secondSignal.aborted).toBe(true);

    await act(async () => latest.resolve([{ id: 'latest', title: 'Latest issue' }]));
    await act(async () => first.resolve([{ id: 'old', title: 'Stale issue' }]));
    await act(async () => second.reject(new Error('Stale error')));
    expect(screen.getByRole('button', { name: 'Latest issue' })).toBeInTheDocument();
    expect(screen.queryByText('Stale issue')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows loading, errors and empty results, and retries the current request', async () => {
    const request = deferred();
    const loadIssues = vi.fn().mockReturnValueOnce(request.promise).mockResolvedValueOnce([]);
    render(<IssueBoard loadIssues={loadIssues} />);
    expect(screen.getByRole('status')).toHaveTextContent('Loading issues...');
    await act(async () => request.reject(new Error('Network unavailable')));
    expect(screen.getByRole('alert')).toHaveTextContent('Network unavailable');

    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(screen.getByRole('status')).toHaveTextContent('Loading issues...');
    await act(async () => {});
    expect(screen.getByRole('status')).toHaveTextContent('No issues found.');
    expect(loadIssues).toHaveBeenCalledTimes(2);
    expect(loadIssues).toHaveBeenLastCalledWith({
      query: '', status: 'all', signal: expect.any(AbortSignal),
    });
  });

  it('preserves selection by ID with refreshed data and clears it when the issue disappears', async () => {
    const loadIssues = vi.fn()
      .mockResolvedValueOnce([{ id: 'one', title: 'Original', description: 'Old details' }])
      .mockResolvedValueOnce([{ id: 'one', title: 'Updated', description: 'New details' }])
      .mockResolvedValueOnce([{ id: 'two', title: 'Other issue' }]);
    render(<IssueBoard loadIssues={loadIssues} />);
    fireEvent.click(await screen.findByRole('button', { name: 'Original' }));
    expect(screen.getByRole('button', { name: 'Original' })).toHaveAttribute('aria-pressed', 'true');
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'open' } });
    expect(await screen.findByRole('button', { name: 'Updated' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('region', { name: 'Selected issue' })).toHaveTextContent('New details');

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'closed' } });
    expect(await screen.findByRole('button', { name: 'Other issue' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.queryByRole('region', { name: 'Selected issue' })).not.toBeInTheDocument();
  });

  it('cleans up requests and debounce timers on unmount, including Strict Mode replay', async () => {
    vi.useFakeTimers();
    const requests = [];
    const loadIssues = vi.fn(() => {
      const request = deferred();
      requests.push(request);
      return request.promise;
    });
    const view = render(<StrictMode><IssueBoard loadIssues={loadIssues} /></StrictMode>);
    expect(loadIssues).toHaveBeenCalledTimes(2);
    expect(loadIssues.mock.calls[0][0].signal.aborted).toBe(true);
    expect(loadIssues.mock.calls[1][0].signal.aborted).toBe(false);
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'pending' } });
    expect(loadIssues.mock.calls[1][0].signal.aborted).toBe(true);
    view.unmount();
    expect(vi.getTimerCount()).toBe(0);
    await act(async () => {
      requests[0].resolve([{ id: 'late', title: 'Late result' }]);
      requests[1].reject(new Error('Late error'));
      vi.advanceTimersByTime(300);
    });
    expect(loadIssues).toHaveBeenCalledTimes(2);
    expect(view.container).toBeEmptyDOMElement();
  });
});