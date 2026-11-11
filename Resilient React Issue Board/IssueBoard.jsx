import React, { useEffect, useId, useState } from 'react';

export default function IssueBoard({ loadIssues }) {
  const controlId = useId();
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [status, setStatus] = useState('all');
  const [retryCount, setRetryCount] = useState(0);
  const [board, setBoard] = useState({
    issues: [],
    selectedId: null,
    phase: 'loading',
    error: '',
  });

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedQuery(query), 300);
    return () => clearTimeout(timer);
  }, [query]);

  useEffect(() => {
    setBoard(previous => ({ ...previous, phase: 'loading', error: '' }));
    if (query !== debouncedQuery) return;

    const controller = new AbortController();
    let active = true;

    async function fetchIssues() {
      try {
        const issues = await loadIssues({
          query: debouncedQuery,
          status,
          signal: controller.signal,
        });
        if (!active) return;
        setBoard(previous => ({
          issues,
          selectedId: issues.some(issue => issue.id === previous.selectedId)
            ? previous.selectedId
            : null,
          phase: 'success',
          error: '',
        }));
      } catch (error) {
        if (!active) return;
        setBoard(previous => ({
          ...previous,
          phase: 'error',
          error: error instanceof Error ? error.message : 'Unable to load issues.',
        }));
      }
    }

    void fetchIssues();
    return () => {
      active = false;
      controller.abort();
    };
  }, [query, debouncedQuery, status, retryCount, loadIssues]);

  const selectedIssue = board.issues.find(issue => issue.id === board.selectedId);

  return (
    <section aria-labelledby={`${controlId}-heading`}>
      <h1 id={`${controlId}-heading`}>Issue Board</h1>
      <label htmlFor={`${controlId}-search`}>Search issues</label>
      <input
        id={`${controlId}-search`}
        type="search"
        value={query}
        onChange={event => setQuery(event.target.value)}
      />
      <label htmlFor={`${controlId}-status`}>Status</label>
      <select
        id={`${controlId}-status`}
        value={status}
        onChange={event => setStatus(event.target.value)}
      >
        <option value="all">All</option>
        <option value="open">Open</option>
        <option value="closed">Closed</option>
      </select>

      <div aria-busy={board.phase === 'loading'}>
        {board.phase === 'loading' && <p role="status">Loading issues...</p>}
        {board.phase === 'error' && (
          <div role="alert">
            <p>{board.error || 'Unable to load issues.'}</p>
            <button type="button" onClick={() => setRetryCount(count => count + 1)}>
              Retry
            </button>
          </div>
        )}
        {board.phase === 'success' && board.issues.length === 0 && (
          <p role="status">No issues found.</p>
        )}
        {board.phase === 'success' && board.issues.length > 0 && (
          <>
            <ul aria-label="Issues">
              {board.issues.map(issue => (
                <li key={issue.id}>
                  <button
                    type="button"
                    aria-pressed={issue.id === board.selectedId}
                    onClick={() => setBoard(previous => ({ ...previous, selectedId: issue.id }))}
                  >
                    {issue.title}
                  </button>
                </li>
              ))}
            </ul>
            {selectedIssue && (
              <section aria-label="Selected issue">
                <h2>{selectedIssue.title}</h2>
                <p>{selectedIssue.description}</p>
              </section>
            )}
          </>
        )}
      </div>
    </section>
  );
}