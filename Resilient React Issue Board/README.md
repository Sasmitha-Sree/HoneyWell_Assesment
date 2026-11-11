# Resilient React Issue Board

A standalone React component with five focused Vitest / React Testing Library tests.

```sh
npm install
npm test
```

## Usage

```jsx
import IssueBoard from './IssueBoard.jsx';

async function loadIssues({ query, status, signal }) {
  const params = new URLSearchParams({ query, status });
  const response = await fetch(`/api/issues?${params}`, { signal });
  if (!response.ok) throw new Error('Unable to load issues.');
  return response.json();
}

<IssueBoard loadIssues={loadIssues} />
```

The loader must return a promise of issues shaped as
`{ id: string, title: string, description?: string }[]`, with unique, stable IDs.
Status values are `all`, `open`, and `closed`. Keep the loader's function identity
stable (for example, define it outside the parent component) to avoid unnecessary reloads.

The initial request and status changes are immediate. Search waits 300 ms after
the last change; editing search aborts the previous request immediately. A status
change during that wait uses the latest status when the search settles.
Cleanup aborts requests and guards both success and failure callbacks even if
the loader ignores its signal. Selection is preserved by ID, uses refreshed
issue data, and clears when the selected issue is absent. No issue is selected
automatically. Retry repeats the current query and status.