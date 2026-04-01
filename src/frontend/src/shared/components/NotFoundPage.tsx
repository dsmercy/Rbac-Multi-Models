import { Link } from 'react-router-dom';

export default function NotFoundPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="text-center space-y-4">
        <h1 className="text-4xl font-bold">404</h1>
        <p className="text-muted-foreground">This page does not exist.</p>
        <Link
          to="/login"
          className="inline-block text-sm text-primary underline underline-offset-4"
        >
          Back to login
        </Link>
      </div>
    </div>
  );
}
