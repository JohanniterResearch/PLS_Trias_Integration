import '../styles/title.css';

export default function Appbar() {
  return (
    <div className="header">
      <div className="logo">
        <img src="/logo.png" alt="Logo" />
      </div>
      <div className="links">
        <a href="/AdminSettingsPage" className="admin-text">Admin</a>
        <a href="/" className="admin-text">Home</a>
      </div>
    </div>
  );
}
