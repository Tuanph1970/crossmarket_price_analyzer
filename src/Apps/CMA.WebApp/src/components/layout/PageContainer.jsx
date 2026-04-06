export default function PageContainer({ children, className = '' }) {
  return <div className={`p-6 ${className}`}>{children}</div>;
}
