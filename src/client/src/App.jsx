import logo from "../assets/logo.svg";

export default function App() {
  return (
    <div className="app-shell">
      <header className="site-header">
        <img className="site-logo" src={logo} alt="KartuOn" />
      </header>
      <main className="placeholder">
        <p className="eyebrow">Sea Battle Paper</p>
        <h1>The game is being prepared.</h1>
        <p>This clean shell will become the new two-player battleship experience.</p>
      </main>
    </div>
  );
}
