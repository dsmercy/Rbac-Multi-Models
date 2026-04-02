import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useGetUsersQuery } from '../userEndpoints';
import { Authorized } from '@/shared/components/Authorized';
import { EmptyState } from '@/shared/components/EmptyState';
import { SkeletonTable } from '@/shared/components/Skeleton';

export default function UserListPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading, isError, refetch } = useGetUsersQuery(
    { tenantId: tenantId!, search: search || undefined, page, pageSize: 20 },
    { skip: !tenantId }
  );

  const users = data?.items ?? [];
  const totalPages = data ? Math.ceil(data.totalCount / data.pageSize) : 1;

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearch(e.target.value);
    setPage(1);
  };

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Users</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            {data ? `${data.totalCount} user${data.totalCount !== 1 ? 's' : ''}` : 'Manage users and role assignments'}
          </p>
        </div>
        <Authorized action="user:create" resource="users">
          <button
            onClick={() => navigate(`/${tenantId}/users/new`)}
            className="px-4 py-2 bg-primary text-primary-foreground text-sm font-medium rounded-md hover:bg-primary/90 transition-colors"
          >
            + New user
          </button>
        </Authorized>
      </div>

      <div className="flex items-center gap-2">
        <input
          type="search"
          value={search}
          onChange={handleSearchChange}
          placeholder="Search by email or name…"
          aria-label="Search users"
          className="w-full max-w-sm border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
      </div>

      {isLoading && <SkeletonTable rows={5} cols={5} />}

      {isError && (
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load users.</span>
          <button onClick={() => void refetch()} className="underline">Retry</button>
        </div>
      )}

      {!isLoading && !isError && users.length === 0 && (
        <EmptyState
          icon="👥"
          title="No users found"
          description={search ? `No users match "${search}".` : 'Add users to get started.'}
          action={
            !search ? (
              <Authorized action="user:create" resource="users">
                <button onClick={() => navigate(`/${tenantId}/users/new`)} className="px-4 py-2 bg-primary text-primary-foreground text-sm rounded-md">
                  Add user
                </button>
              </Authorized>
            ) : undefined
          }
        />
      )}

      {users.length > 0 && (
        <>
          <div className="border rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50 text-muted-foreground">
                <tr>
                  <th className="text-left px-4 py-3 font-medium">Email</th>
                  <th className="text-left px-4 py-3 font-medium">Display name</th>
                  <th className="text-left px-4 py-3 font-medium">Status</th>
                  <th className="text-left px-4 py-3 font-medium">Created</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {users.map((user) => (
                  <tr key={user.id} className="hover:bg-muted/30 transition-colors">
                    <td className="px-4 py-3 font-medium">{user.email}</td>
                    <td className="px-4 py-3 text-muted-foreground">{user.displayName || '—'}</td>
                    <td className="px-4 py-3">
                      {user.isActive
                        ? <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full">Active</span>
                        : <span className="text-xs bg-red-100 text-red-600 px-2 py-0.5 rounded-full">Inactive</span>}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">{new Date(user.createdAt).toLocaleDateString()}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-2">
                        <Authorized action="user:update" resource="users">
                          <button
                            onClick={() => navigate(`/${tenantId}/users/${user.id}`)}
                            className="text-xs px-3 py-1 border rounded hover:bg-accent transition-colors"
                          >
                            Manage
                          </button>
                        </Authorized>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">
                Page {page} of {totalPages}
              </span>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  aria-label="Previous page"
                  className="px-3 py-1.5 border rounded-md hover:bg-accent disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Previous
                </button>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                  aria-label="Next page"
                  className="px-3 py-1.5 border rounded-md hover:bg-accent disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
