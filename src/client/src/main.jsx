import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { QRCodeSVG } from 'qrcode.react';
import { BrowserRouter, Link, Navigate, Route, Routes, useNavigate, useParams } from 'react-router-dom';
import './styles.css';

const BOARD_SIZE = 10;
const FLEET = [4, 3, 3, 2, 2, 2, 1, 1, 1, 1];
const FLEET_GROUPS = [
  { length: 4, count: 1, label: '4-cell ship' },
  { length: 3, count: 2, label: '3-cell ships' },
  { length: 2, count: 3, label: '2-cell ships' },
  { length: 1, count: 4, label: '1-cell ships' },
];
const SHAPES = [
  { id: 'single', length: 1, offsets: [[0, 0]] },
  { id: 'line-2', length: 2, offsets: [[0, 0], [0, 1]] },
  { id: 'line-3', length: 3, offsets: [[0, 0], [0, 1], [0, 2]] },
  { id: 'corner-3', length: 3, offsets: [[0, 0], [1, 0], [1, 1]] },
  { id: 'line-4', length: 4, offsets: [[0, 0], [0, 1], [0, 2], [0, 3]] },
  { id: 'box-4', length: 4, offsets: [[0, 0], [0, 1], [1, 0], [1, 1]] },
  { id: 't-4', length: 4, offsets: [[0, 0], [0, 1], [0, 2], [1, 1]] },
  { id: 'l-4', length: 4, offsets: [[0, 0], [1, 0], [2, 0], [2, 1]] },
  { id: 'j-4', length: 4, offsets: [[0, 1], [1, 1], [2, 0], [2, 1]] },
  { id: 's-4', length: 4, offsets: [[0, 1], [0, 2], [1, 0], [1, 1]] },
  { id: 'z-4', length: 4, offsets: [[0, 0], [0, 1], [1, 1], [1, 2]] },
];

function tokenKey(matchId) {
  return `sea-battle-paper:${matchId}:player-token`;
}

function getToken(matchId) {
  return localStorage.getItem(tokenKey(matchId));
}

function saveToken(matchId, token) {
  localStorage.setItem(tokenKey(matchId), token);
}

function normalizeFleet(fleet) {
  return [...(fleet ?? [])]
    .map((ship) => ({
      row: ship.row,
      column: ship.column,
      length: ship.length,
      isHorizontal: ship.isHorizontal,
      cellOffsets: normalizeOffsets(getOffsets(ship)),
    }))
    .sort((left, right) =>
      left.length - right.length
      || left.row - right.row
      || left.column - right.column
      || Number(left.isHorizontal) - Number(right.isHorizontal)
      || offsetSignature(left.cellOffsets).localeCompare(offsetSignature(right.cellOffsets)));
}

function fleetSignature(fleet) {
  return JSON.stringify(normalizeFleet(fleet));
}

function cellsFor(ship) {
  return getOffsets(ship).map((offset) => ({
    row: ship.row + offset.row,
    column: ship.column + offset.column,
  }));
}

function getOffsets(ship) {
  if (ship.cellOffsets?.length) {
    return normalizeOffsets(ship.cellOffsets);
  }

  return Array.from({ length: ship.length }, (_, index) => ({
    row: ship.isHorizontal ? 0 : index,
    column: ship.isHorizontal ? index : 0,
  }));
}

function normalizeOffsets(offsets) {
  const rows = offsets.map((offset) => offset.row ?? offset[0]);
  const columns = offsets.map((offset) => offset.column ?? offset[1]);
  const minRow = Math.min(...rows);
  const minColumn = Math.min(...columns);
  return offsets
    .map((offset) => ({
      row: (offset.row ?? offset[0]) - minRow,
      column: (offset.column ?? offset[1]) - minColumn,
    }))
    .sort((left, right) => left.row - right.row || left.column - right.column);
}

function offsetSignature(offsets) {
  return normalizeOffsets(offsets).map((offset) => `${offset.row}:${offset.column}`).join(';');
}

function shapeOptions(mode, length) {
  if (mode === 'paper') {
    return SHAPES.filter((shape) => shape.length === length);
  }

  return [SHAPES.find((shape) => shape.id === `line-${length}`) ?? SHAPES[0]];
}

function findShape(shapeId) {
  return SHAPES.find((shape) => shape.id === shapeId);
}

function createShip(row, column, shape) {
  const cellOffsets = normalizeOffsets(shape.offsets);
  return {
    row,
    column,
    length: shape.length,
    isHorizontal: isMostlyHorizontal(cellOffsets),
    cellOffsets,
    shapeId: shape.id,
  };
}

function isMostlyHorizontal(offsets) {
  const rows = new Set(offsets.map((offset) => offset.row));
  const columns = new Set(offsets.map((offset) => offset.column));
  return columns.size >= rows.size;
}

function rotateOffsets(offsets) {
  return normalizeOffsets(offsets.map((offset) => ({
    row: offset.column,
    column: -offset.row,
  })));
}

function validateFleet(fleet, mode = 'classic') {
  const sorted = fleet.map((ship) => ship.length).sort((a, b) => b - a);
  if (sorted.join(',') !== FLEET.join(',')) {
    return 'Place the full classic fleet first.';
  }

  return validateFleetShape(fleet, mode);
}

function validateFleetShape(fleet, mode = 'classic') {
  const counts = fleet.reduce((map, ship) => map.set(ship.length, (map.get(ship.length) ?? 0) + 1), new Map());
  for (const [length, count] of counts) {
    if (count > FLEET.filter((shipLength) => shipLength === length).length || !FLEET.includes(length)) {
      return 'Fleet has too many ships of one length.';
    }
  }

  const occupied = new Map();
  for (const ship of fleet) {
    const offsets = getOffsets(ship);
    if (offsets.length !== ship.length) {
      return 'Ship shape must match ship length.';
    }

    if (mode === 'classic' && !isStraight(offsets)) {
      return 'Classic ships must be straight.';
    }

    for (const cell of cellsFor(ship)) {
      if (cell.row < 0 || cell.row >= BOARD_SIZE || cell.column < 0 || cell.column >= BOARD_SIZE) {
        return 'Ships must stay inside the board.';
      }

      const key = `${cell.row}:${cell.column}`;
      if (occupied.has(key)) {
        return 'Ships cannot overlap.';
      }

      occupied.set(key, ship);
    }
  }

  for (const ship of fleet) {
    for (const cell of cellsFor(ship)) {
      for (let row = cell.row - 1; row <= cell.row + 1; row += 1) {
        for (let column = cell.column - 1; column <= cell.column + 1; column += 1) {
          const neighbor = occupied.get(`${row}:${column}`);
          if (neighbor && neighbor !== ship) {
            return 'Ships cannot touch, including corners.';
          }
        }
      }
    }
  }

  return null;
}

function isStraight(offsets) {
  return new Set(offsets.map((offset) => offset.row)).size === 1 || new Set(offsets.map((offset) => offset.column)).size === 1;
}

function tryAddShip(fleet, ship, mode) {
  const error = validateFleetShape([...fleet, ship], mode);
  return error ? null : [...fleet, ship];
}

function countPlacedShips(fleet, length) {
  return fleet.filter((ship) => ship.length === length).length;
}

function allowedShipCount(length) {
  return FLEET.filter((shipLength) => shipLength === length).length;
}

function findShipAt(fleet, row, column) {
  return fleet.find((ship) => cellsFor(ship).some((cell) => cell.row === row && cell.column === column));
}

function tryRotateShip(fleet, ship, mode) {
  const cellOffsets = rotateOffsets(getOffsets(ship));
  const rotated = { ...ship, cellOffsets, isHorizontal: isMostlyHorizontal(cellOffsets) };
  const next = fleet.map((candidate) => (candidate === ship ? rotated : candidate));
  return validateFleetShape(next, mode) ? null : next;
}

function randomFleet(mode = 'classic') {
  for (let attempt = 0; attempt < 500; attempt += 1) {
    let fleet = [];
    let failed = false;
    for (const length of FLEET) {
      const options = shapeOptions(mode, length);
      let placed = false;
      for (let shipAttempt = 0; shipAttempt < 100; shipAttempt += 1) {
        let ship = createShip(0, 0, options[Math.floor(Math.random() * options.length)]);
        const rotationCount = Math.floor(Math.random() * 4);
        for (let rotation = 0; rotation < rotationCount; rotation += 1) {
          const cellOffsets = rotateOffsets(ship.cellOffsets);
          ship = { ...ship, cellOffsets, isHorizontal: isMostlyHorizontal(cellOffsets) };
        }
        const maxRow = Math.max(...ship.cellOffsets.map((offset) => offset.row));
        const maxColumn = Math.max(...ship.cellOffsets.map((offset) => offset.column));
        const row = Math.floor(Math.random() * (BOARD_SIZE - maxRow));
        const column = Math.floor(Math.random() * (BOARD_SIZE - maxColumn));
        const next = tryAddShip(fleet, { ...ship, row, column }, mode);
        if (next) {
          fleet = next;
          placed = true;
          break;
        }
      }

      if (!placed) {
        failed = true;
        break;
      }
    }

    if (!failed && !validateFleet(fleet, mode)) {
      return fleet;
    }
  }

  return [];
}

function Header() {
  return (
    <header className="app-header">
      <Link to="/" className="brand" aria-label="KartuOn Sea Battle Paper">
        <span className="brand-mark">K</span>
        <span>KartuOn</span>
      </Link>
    </header>
  );
}

function SetupPage() {
  const navigate = useNavigate();
  const [tab, setTab] = useState('classic');
  const [revealSunkShips, setRevealSunkShips] = useState(true);
  const [isStarting, setIsStarting] = useState(false);
  const [error, setError] = useState('');

  async function startMatch() {
    setIsStarting(true);
    setError('');
    try {
    const response = await fetch('/ship-api/matches', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ mode: tab, revealSunkShips }),
      });
      const payload = await response.json();
      if (!response.ok) {
        throw new Error(payload.message ?? 'Could not create match.');
      }

      saveToken(payload.matchId, payload.playerToken);
      navigate(`/match/${payload.matchId}/lobby`);
    } catch (err) {
      setError(err.message);
    } finally {
      setIsStarting(false);
    }
  }

  return (
    <>
      <Header />
      <main className="setup-page">
        <section className="setup-copy">
          <p className="eyebrow">Sea Battle Paper</p>
          <h1>Set up a two-player match.</h1>
          <p>
            Start a free match, share the invite link, place ships, and play in real time. No login is needed.
          </p>
        </section>

        <section className="setup-panel" aria-label="Game setup">
          <div className="tabs" role="tablist">
            <button className={tab === 'classic' ? 'active' : ''} onClick={() => setTab('classic')} type="button">
              Classic
            </button>
            <button className={tab === 'paper' ? 'active' : ''} onClick={() => setTab('paper')} type="button">
              Paper
            </button>
          </div>

          {tab === 'classic' ? (
            <div className="rules">
              <h2>Classic rules</h2>
              <p>10 × 10 board with 1 × 4-cell ship, 2 × 3-cell ships, 3 × 2-cell ships, and 4 × 1-cell ships.</p>
              <p>Ships cannot overlap or touch by side or corner.</p>
              <p>Hit or sink keeps your turn. Miss passes the turn.</p>
              <p>Sink all enemy ships to win!</p>
            </div>
          ) : (
            <div className="rules">
              <h2>Paper rules</h2>
              <p>10 × 10 board with 1 × 4-cell ship, 2 × 3-cell ships, 3 × 2-cell ships, and 4 × 1-cell ships.</p>
              <p><strong>3-cell and 4-cell ships may use corner, box, L, T, S, or Z shapes.</strong></p>
              <p>Ships cannot overlap or touch by side or corner.</p>
              <p>Hit or sink keeps your turn. Miss passes the turn.</p>
              <p>Sink all enemy ships to win!</p>
            </div>
          )}

          <div className="setup-option-group" aria-label="Sunk ship reveal setting">
            <h2>Sunk ship result</h2>
            <label className={`setup-option ${revealSunkShips ? 'selected' : ''}`}>
              <input
                checked={revealSunkShips}
                name="revealSunkShips"
                onChange={() => setRevealSunkShips(true)}
                type="radio"
              />
              <span>
                <strong>Automated</strong>
                <small>Show when a ship is sunk and reveal its occupied cells.</small>
              </span>
            </label>
            <label className={`setup-option ${!revealSunkShips ? 'selected' : ''}`}>
              <input
                checked={!revealSunkShips}
                name="revealSunkShips"
                onChange={() => setRevealSunkShips(false)}
                type="radio"
              />
              <span>
                <strong>Manual</strong>
                <small>Keep sunk ships hidden. Hits stay as hits until the game ends.</small>
              </span>
            </label>
          </div>

          <div className="setup-action">
            {error ? <p className="error">{error}</p> : null}
            <button className="primary setup-start" disabled={isStarting} onClick={startMatch} type="button">
              {isStarting ? 'STARTING...' : 'START'}
            </button>
          </div>
        </section>
      </main>
    </>
  );
}

function useSeaBattleConnection(matchId) {
  const [connectionState, setConnectionState] = useState('connecting');
  const [state, setState] = useState(null);
  const [join, setJoin] = useState(null);
  const [error, setError] = useState('');
  const connectionRef = useRef(null);

  useEffect(() => {
    let disposed = false;
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/sea-battle')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = connection;

    connection.on('JoinAccepted', (payload) => {
      saveToken(payload.matchId, payload.playerToken);
      setJoin(payload);
      setConnectionState('joined');
    });
    connection.on('JoinRejected', (payload) => {
      setError(payload.message ?? 'Could not join match.');
      setConnectionState('rejected');
    });
    connection.on('StateUpdated', (payload) => setState(payload));
    connection.on('Error', (payload) => setError(payload.message ?? 'Something went wrong.'));
    connection.on('PlayerRemoved', () => {
      localStorage.removeItem(tokenKey(matchId));
      setError('You were removed from this lobby.');
      setConnectionState('removed');
    });
    connection.onreconnecting(() => setConnectionState('reconnecting'));
    connection.onreconnected(async () => {
      await connection.invoke('JoinMatch', matchId, getToken(matchId));
    });

    async function connect() {
      try {
        await connection.start();
        if (!disposed) {
          await connection.invoke('JoinMatch', matchId, getToken(matchId));
        }
      } catch (err) {
        if (!disposed) {
          setError(err.message);
          setConnectionState('rejected');
        }
      }
    }

    connect();

    return () => {
      disposed = true;
      connection.stop();
    };
  }, [matchId]);

  const invoke = useCallback(async (method, ...args) => {
    setError('');
    try {
      await connectionRef.current?.invoke(method, ...args);
    } catch (err) {
      setError(err.message);
    }
  }, []);

  return { connectionState, state, join, error, invoke };
}

function MatchPage() {
  const { matchId } = useParams();
  const { connectionState, state, join, error, invoke } = useSeaBattleConnection(matchId);
  const path = window.location.pathname;
  const isResultPath = path.endsWith('/result');

  if (connectionState === 'removed') {
    return <Navigate to="/" replace />;
  }

  if (!state) {
    return (
      <>
        <Header />
        <main className="center-status">{error || 'Joining match...'}</main>
      </>
    );
  }

  if (state.status === 'InProgress') {
    return <BattleView state={state} invoke={invoke} error={error} />;
  }

  if (state.status === 'Finished' || isResultPath) {
    return <ResultView state={state} />;
  }

  return (
    <>
      <Header />
      <LobbyView matchId={matchId} state={state} join={join} error={error} invoke={invoke} />
    </>
  );
}

function LobbyView({ matchId, state, join, error, invoke }) {
  const [fleet, setFleet] = useState([]);
  const [nickname, setNickname] = useState('');
  const [selectedShapeId, setSelectedShapeId] = useState(null);
  const [placementPreview, setPlacementPreview] = useState(null);
  const [linkCopied, setLinkCopied] = useState(false);
  const savedFleetSignatureRef = useRef(fleetSignature([]));
  const didHydrateFleetRef = useRef(false);
  const saveFleetTimeoutRef = useRef(null);
  const viewer = state.players.find((player) => player.id === state.viewerPlayerId);
  const opponent = state.players.find((player) => player.id !== state.viewerPlayerId);
  const placedByLength = useMemo(() => {
    return fleet.reduce((map, ship) => map.set(ship.length, (map.get(ship.length) ?? 0) + 1), new Map());
  }, [fleet]);
  const fleetError = validateFleet(fleet, state.mode);
  const visibleFleetError = fleetError === 'Place the full classic fleet first.' ? null : fleetError;
  const inviteUrl = `${window.location.origin}/match/${matchId}/lobby`;

  async function copyInviteLink() {
    try {
      await navigator.clipboard.writeText(inviteUrl);
      setLinkCopied(true);
      window.setTimeout(() => setLinkCopied(false), 1800);
    } catch {
      setLinkCopied(false);
    }
  }

  useEffect(() => {
    setNickname(viewer?.nickname ?? '');
  }, [viewer?.nickname]);

  useEffect(() => {
    const selectedShape = selectedShapeId ? findShape(selectedShapeId) : null;
    if (selectedShape && countPlacedShips(fleet, selectedShape.length) >= allowedShipCount(selectedShape.length)) {
      setSelectedShapeId(null);
    }
  }, [fleet, selectedShapeId]);

  useEffect(() => {
    if (didHydrateFleetRef.current || state.status !== 'Lobby') {
      return;
    }

    const savedFleet = normalizeFleet(state.myShips);
    savedFleetSignatureRef.current = fleetSignature(savedFleet);
    setFleet(savedFleet);
    didHydrateFleetRef.current = true;
  }, [state.myShips, state.status]);

  useEffect(() => {
    if (!didHydrateFleetRef.current || viewer?.isReady || state.status !== 'Lobby') {
      return undefined;
    }

    const nextSignature = fleetSignature(fleet);
    if (nextSignature === savedFleetSignatureRef.current) {
      return undefined;
    }

    saveFleetTimeoutRef.current = window.setTimeout(() => {
      savedFleetSignatureRef.current = nextSignature;
      invoke('SaveFleetDraft', fleet);
      saveFleetTimeoutRef.current = null;
    }, 350);

    return () => {
      if (saveFleetTimeoutRef.current) {
        window.clearTimeout(saveFleetTimeoutRef.current);
        saveFleetTimeoutRef.current = null;
      }
    };
  }, [fleet, invoke, state.status, viewer?.isReady]);

  function readyUp() {
    if (saveFleetTimeoutRef.current) {
      window.clearTimeout(saveFleetTimeoutRef.current);
      saveFleetTimeoutRef.current = null;
    }

    savedFleetSignatureRef.current = fleetSignature(fleet);
    invoke('ReadyUp', fleet);
  }

  function saveNickname() {
    const nextNickname = nickname.trim();
    if (!nextNickname || nextNickname === viewer?.nickname || viewer?.isReady) {
      return;
    }

    invoke('UpdateNickname', nextNickname);
  }

  function addShipAt(row, column, shape) {
    if (viewer?.isReady) {
      return false;
    }

    if (!shape) {
      return false;
    }

    const length = shape.length;
    if (!FLEET.includes(length) || countPlacedShips(fleet, length) >= allowedShipCount(length)) {
      return false;
    }

    const next = tryAddShip(fleet, createShip(row, column, shape), state.mode);
    if (!next) {
      return false;
    }

    setFleet(next);
    setPlacementPreview(null);
    if (countPlacedShips(next, length) >= allowedShipCount(length)) {
      setSelectedShapeId(null);
    }

    return true;
  }

  function interactWithCell(row, column) {
    if (viewer?.isReady) {
      return;
    }

    const existingShip = findShipAt(fleet, row, column);
    if (existingShip) {
      const rotated = tryRotateShip(fleet, existingShip, state.mode);
      if (rotated) {
        setFleet(rotated);
      }

      return;
    }

    if (selectedShapeId) {
      addShipAt(row, column, findShape(selectedShapeId));
    }
  }

  function dragShip(event, shapeId) {
    event.dataTransfer.effectAllowed = 'copy';
    event.dataTransfer.setData('text/plain', `new:${shapeId}`);
  }

  function moveShip(row, column, shipIndex, rowOffset, columnOffset) {
    if (viewer?.isReady) {
      return false;
    }

    const ship = fleet[shipIndex];
    if (!ship) {
      return false;
    }

    const movedShip = {
      ...ship,
      row: row - rowOffset,
      column: column - columnOffset,
    };
    const next = fleet.map((candidate, index) => (index === shipIndex ? movedShip : candidate));
    if (validateFleetShape(next, state.mode)) {
      return false;
    }

    setFleet(next);
    setPlacementPreview(null);
    return true;
  }

  function removeShip(shipIndex) {
    if (viewer?.isReady || !fleet[shipIndex]) {
      return;
    }

    setFleet(fleet.filter((_, index) => index !== shipIndex));
    setPlacementPreview(null);
  }

  function previewNewShip(row, column, event) {
    const dragData = event.dataTransfer.getData('text/plain');
    const [kind, firstValue] = dragData.split(':');
    const shape = findShape(firstValue);
    if (kind !== 'new' || !shape) {
      setPlacementPreview(null);
      return;
    }

    const ship = createShip(row, column, shape);
    const isValid = FLEET.includes(shape.length)
      && countPlacedShips(fleet, shape.length) < allowedShipCount(shape.length)
      && !validateFleetShape([...fleet, ship], state.mode);
    setPlacementPreview({ ship, isValid });
  }

  function previewMovedShip(row, column, shipIndex, rowOffset, columnOffset) {
    const ship = fleet[shipIndex];
    if (!ship) {
      setPlacementPreview(null);
      return;
    }

    const movedShip = {
      ...ship,
      row: row - rowOffset,
      column: column - columnOffset,
    };
    const next = fleet.map((candidate, index) => (index === shipIndex ? movedShip : candidate));
    setPlacementPreview({ ship: movedShip, isValid: !validateFleetShape(next, state.mode) });
  }

  function dropShip(row, column, event) {
    const dragData = event.dataTransfer.getData('text/plain');
    const [kind, firstValue] = dragData.split(':');

    if (kind === 'new') {
      addShipAt(row, column, findShape(firstValue));
    }
  }

  return (
    <main className="lobby-page">
      <section className="lobby-top">
        <div>
          <p className="eyebrow">Lobby {matchId}</p>
          <h1>Place your fleet.</h1>
          <p>Share the invite link. When both players are ready, the match starts automatically.</p>
        </div>
        <div className="invite-box">
          <div className="invite-option qr-option">
            <QRCodeSVG value={inviteUrl} size={112} />
            <span>Scan QR</span>
          </div>
          <div className="invite-option copy-option">
            <span className="invite-url">{inviteUrl}</span>
            <button className="primary copy-link-button" onClick={copyInviteLink} type="button">
              {linkCopied ? 'Copied' : 'Copy link'}
            </button>
          </div>
        </div>
      </section>

      <section className="players-strip">
        {state.players.map((player) => (
          <div className="player-pill" key={player.id}>
            <span>{player.nickname}</span>
            <small>{player.isOnline ? 'online' : 'offline'} · {player.isReady ? 'ready' : 'placing'}</small>
          </div>
        ))}
        {!opponent && <div className="player-pill muted">Waiting for opponent</div>}
      </section>

      <section className="placement-grid">
        <div className="placement-left">
          <div className="placement-tools">
            <label>
              Nickname
              <input
                value={nickname}
                disabled={viewer?.isReady}
                onBlur={saveNickname}
                onChange={(event) => setNickname(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.currentTarget.blur();
                  }
                }}
              />
            </label>
            <button type="button" disabled={viewer?.isReady} onClick={() => setFleet(randomFleet(state.mode))}>
              Randomize
            </button>
            <button type="button" disabled={viewer?.isReady} onClick={() => { setFleet([]); setSelectedShapeId(null); setPlacementPreview(null); }}>
              Clear
            </button>
            <button
              className={`primary ready-action ${!viewer?.isReady && !fleetError ? 'ready-action-pulse' : ''}`}
              type="button"
              disabled={viewer?.isReady || !!fleetError}
              onClick={readyUp}
            >
              {viewer?.isReady ? 'Ready locked' : 'Ready'}
            </button>
            {state.viewerSeat === 1 && opponent && !opponent.isReady ? (
              <button type="button" onClick={() => invoke('RemoveOpponent')}>
                Remove opponent
              </button>
            ) : null}
            {visibleFleetError ? <p className="error">{visibleFleetError}</p> : null}
            {error ? <p className="error">{error}</p> : null}
          </div>
          <div className="placement-rules">
            <h2>Rules</h2>
            <p>Drag a ship onto the board. Drag placed ships to move them, drag off the board to remove them, or tap to rotate if legal.</p>
            <p>Ships cannot overlap or touch by side or corner.</p>
          </div>
        </div>
        <Board
          ships={fleet}
          cells={[]}
          mode="placement"
          onCell={interactWithCell}
          onClearPreview={() => setPlacementPreview(null)}
          onDropCell={dropShip}
          onPreviewDropCell={previewNewShip}
          onPreviewShipMove={previewMovedShip}
          onShipRemove={removeShip}
          onShipMove={moveShip}
          preview={placementPreview}
        />
        <aside className="placement-side">
          <div className="ship-palette" aria-label="Ships to place">
            {FLEET_GROUPS.map((group) => {
              const placed = placedByLength.get(group.length) ?? 0;
              const canPlace = !viewer?.isReady && placed < group.count;

              return (
                <section className="ship-group" key={group.length}>
                  <div className="ship-group-title">
                    <span>{group.label}</span>
                    <small>{placed}/{group.count}</small>
                  </div>
                  <div className="ship-options">
                    {shapeOptions(state.mode, group.length).map((shape) => {
                      const offsets = normalizeOffsets(shape.offsets);
                      const maxRow = Math.max(...offsets.map((offset) => offset.row));
                      const maxColumn = Math.max(...offsets.map((offset) => offset.column));

                      return (
                        <button
                          aria-pressed={selectedShapeId === shape.id && canPlace}
                          className={`palette-ship ${selectedShapeId === shape.id && canPlace ? 'selected' : ''}`}
                          disabled={!canPlace}
                          draggable={canPlace}
                          key={shape.id}
                          onClick={() => setSelectedShapeId(shape.id)}
                          onDragStart={(event) => dragShip(event, shape.id)}
                          style={{
                            gridTemplateColumns: `repeat(${maxColumn + 1}, 22px)`,
                            gridTemplateRows: `repeat(${maxRow + 1}, 22px)`,
                          }}
                          type="button"
                        >
                          {offsets.map((offset) => (
                            <span
                              className="palette-cell"
                              key={`${offset.row}:${offset.column}`}
                              style={{
                                gridColumn: offset.column + 1,
                                gridRow: offset.row + 1,
                              }}
                            />
                          ))}
                        </button>
                      );
                    })}
                  </div>
                </section>
              );
            })}
          </div>
        </aside>
      </section>
    </main>
  );
}

function BattleView({ state, invoke, error }) {
  const [mobileBoard, setMobileBoard] = useState('enemy');
  const viewer = state.players.find((player) => player.id === state.viewerPlayerId);
  const opponent = state.players.find((player) => player.id !== state.viewerPlayerId);
  const isMyTurn = state.currentTurnPlayerId === state.viewerPlayerId;
  const winner = state.players.find((player) => player.id === state.winnerPlayerId);

  return (
    <main className="battle-page">
      <section className="battle-status">
        <div>
          <p className="eyebrow">Match {state.matchId}</p>
          <h1>{state.status === 'Finished' ? `${winner?.nickname ?? 'Winner'} wins` : isMyTurn ? 'Your turn' : `${opponent?.nickname ?? 'Opponent'} is firing`}</h1>
        </div>
        <Link className="secondary-link battle-leave" to="/">Leave</Link>
      </section>

      <div className="mobile-tabs">
        <button className={mobileBoard === 'enemy' ? 'active' : ''} onClick={() => setMobileBoard('enemy')} type="button">Opponent</button>
        <button className={mobileBoard === 'mine' ? 'active' : ''} onClick={() => setMobileBoard('mine')} type="button">My fleet</button>
      </div>

      <section className="battle-boards">
        <div className={`board-panel enemy ${mobileBoard === 'enemy' ? 'show' : ''}`}>
          <h2>{opponent?.nickname ?? 'Opponent'}</h2>
          <Board
            ships={state.opponentShips}
            cells={state.opponentBoard}
            disabled={!isMyTurn || state.status !== 'InProgress'}
            mode="enemy"
            onCell={(row, column) => invoke('Fire', row, column)}
          />
        </div>
        <div className={`board-panel mine ${mobileBoard === 'mine' ? 'show' : ''}`}>
          <h2>{viewer?.nickname ?? 'You'}</h2>
          <Board ships={state.myShips} cells={state.myBoard} mode="mine" />
        </div>
      </section>
      {error ? <p className="error battle-error">{error}</p> : null}
    </main>
  );
}

function ResultView({ state }) {
  const winner = state.players.find((player) => player.id === state.winnerPlayerId);

  return (
    <main className="result-page">
      <p className="eyebrow">Final result</p>
      <h1>{winner ? `${winner.nickname} won` : 'Match result'}</h1>
      <section className="battle-boards result">
        <div className="board-panel show">
          <h2>My fleet</h2>
          <Board ships={state.myShips} cells={state.myBoard} mode="mine" />
        </div>
        <div className="board-panel show">
          <h2>Opponent fleet</h2>
          <Board ships={state.opponentShips} cells={state.opponentBoard} mode="mine" />
        </div>
      </section>
      <div className="result-action">
        <Link className="primary as-link" to="/">New match</Link>
      </div>
    </main>
  );
}

function Board({
  ships,
  cells,
  onCell,
  onClearPreview,
  onDropCell,
  onPreviewDropCell,
  onPreviewShipMove,
  onShipMove,
  onShipRemove,
  preview,
  disabled = false,
  mode,
}) {
  const boardRef = useRef(null);
  const dragRef = useRef(null);
  const suppressClickRef = useRef(false);
  const [draggingShipIndex, setDraggingShipIndex] = useState(null);
  const cellMap = useMemo(() => new Map(cells.map((cell) => [`${cell.row}:${cell.column}`, cell])), [cells]);
  const shipCells = useMemo(() => {
    const map = new Map();
    for (const [shipIndex, ship] of (ships ?? []).entries()) {
      for (const cell of cellsFor(ship)) {
        map.set(`${cell.row}:${cell.column}`, { ship, shipIndex });
      }
    }

    return map;
  }, [ships]);
  const previewCells = useMemo(() => {
    const map = new Map();
    if (!preview?.ship) {
      return map;
    }

    for (const cell of cellsFor(preview.ship)) {
      map.set(`${cell.row}:${cell.column}`, preview.isValid ? 'valid' : 'invalid');
    }

    return map;
  }, [preview]);

  function getPointerCell(event) {
    const board = boardRef.current;
    if (!board) {
      return null;
    }

    const rect = board.getBoundingClientRect();
    const column = Math.floor(((event.clientX - rect.left) / rect.width) * BOARD_SIZE);
    const row = Math.floor(((event.clientY - rect.top) / rect.height) * BOARD_SIZE);

    if (row < 0 || row >= BOARD_SIZE || column < 0 || column >= BOARD_SIZE) {
      return null;
    }

    return { row, column };
  }

  function startShipMove(event, shipCell, row, column) {
    if (!shipCell || !onShipMove || disabled) {
      return;
    }

    event.currentTarget.setPointerCapture?.(event.pointerId);
    dragRef.current = {
      pointerId: event.pointerId,
      shipIndex: shipCell.shipIndex,
      rowOffset: row - shipCell.ship.row,
      columnOffset: column - shipCell.ship.column,
      startX: event.clientX,
      startY: event.clientY,
      moved: false,
    };
    setDraggingShipIndex(shipCell.shipIndex);
  }

  function movePointer(event) {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    if (Math.abs(event.clientX - drag.startX) > 4 || Math.abs(event.clientY - drag.startY) > 4) {
      drag.moved = true;
      event.preventDefault();
      const targetCell = getPointerCell(event);
      if (targetCell) {
        onPreviewShipMove?.(targetCell.row, targetCell.column, drag.shipIndex, drag.rowOffset, drag.columnOffset);
      } else {
        onClearPreview?.();
      }
    }
  }

  function finishShipMove(event) {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    event.currentTarget.releasePointerCapture?.(event.pointerId);
    dragRef.current = null;
    setDraggingShipIndex(null);
    onClearPreview?.();

    if (!drag.moved) {
      return;
    }

    suppressClickRef.current = true;
    const targetCell = getPointerCell(event);
    if (targetCell) {
      onShipMove?.(targetCell.row, targetCell.column, drag.shipIndex, drag.rowOffset, drag.columnOffset);
    } else {
      onShipRemove?.(drag.shipIndex);
    }
  }

  function handleCellClick(row, column) {
    if (suppressClickRef.current) {
      suppressClickRef.current = false;
      return;
    }

    onCell?.(row, column);
  }

  return (
    <div className={`paper-board ${mode}`} ref={boardRef}>
      {Array.from({ length: BOARD_SIZE * BOARD_SIZE }, (_, index) => {
        const row = Math.floor(index / BOARD_SIZE);
        const column = index % BOARD_SIZE;
        const cell = cellMap.get(`${row}:${column}`);
        const shipCell = shipCells.get(`${row}:${column}`);
        const ship = shipCell?.ship;
        const previewKind = previewCells.get(`${row}:${column}`);
        const fired = !!cell?.shotResult;
        const className = [
          'board-cell',
          ship ? 'ship' : '',
          draggingShipIndex === shipCell?.shipIndex ? 'dragging' : '',
          previewKind === 'valid' ? 'preview-valid' : '',
          previewKind === 'invalid' ? 'preview-invalid' : '',
          cell?.shotResult === 'Miss' ? 'miss' : '',
          cell?.shotResult === 'Hit' ? 'hit' : '',
          cell?.shotResult === 'Sunk' ? 'sunk' : '',
        ].join(' ');

        return (
          <button
            aria-label={`Row ${row + 1}, column ${column + 1}`}
            className={className}
            disabled={disabled || fired || (!onCell && !onDropCell)}
            key={`${row}:${column}`}
            onClick={() => handleCellClick(row, column)}
            onDragOver={onDropCell ? (event) => {
              event.preventDefault();
              event.dataTransfer.dropEffect = 'copy';
              onPreviewDropCell?.(row, column, event);
            } : undefined}
            onDragLeave={onDropCell ? () => onClearPreview?.() : undefined}
            onDrop={onDropCell ? (event) => {
              event.preventDefault();
              onClearPreview?.();
              onDropCell(row, column, event);
            } : undefined}
            onPointerCancel={finishShipMove}
            onPointerDown={shipCell && onShipMove ? (event) => startShipMove(event, shipCell, row, column) : undefined}
            onPointerMove={onShipMove ? movePointer : undefined}
            onPointerUp={onShipMove ? finishShipMove : undefined}
            type="button"
          >
            {cell?.shotResult === 'Miss' ? '·' : cell?.shotResult ? 'x' : ship ? '' : ''}
          </button>
        );
      })}
    </div>
  );
}

function PrivacyPage() {
  return (
    <>
      <Header />
      <main className="privacy-page">
        <h1>Privacy</h1>
        <p>This standalone test stores anonymous match data and a browser-local player token. No registration is required.</p>
      </main>
    </>
  );
}

function App() {
  return (
    <BrowserRouter basename="/sea-battle-paper">
    {/* <BrowserRouter> */}
      <Routes>
        <Route path="/" element={<SetupPage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/match/:matchId" element={<MatchPage />} />
        <Route path="/match/:matchId/lobby" element={<MatchPage />} />
        <Route path="/match/:matchId/play" element={<MatchPage />} />
        <Route path="/match/:matchId/result" element={<MatchPage />} />
      </Routes>
    </BrowserRouter>
  );
}

createRoot(document.getElementById('root')).render(<App />);
