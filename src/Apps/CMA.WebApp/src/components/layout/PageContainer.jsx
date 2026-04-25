export default function PageContainer({ children, className = '' }) {
  return <div className={`p-6 animate-fade-in ${className}`}>{children}</div>;
}
