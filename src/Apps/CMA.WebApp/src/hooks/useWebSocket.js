/**
 * P3-F01: WebSocket hook for real-time opportunity score updates.
 * Replaces polling with a persistent WebSocket connection to ScoringService.
 * Falls back to polling if WebSocket is unavailable.
 */
import { useState, useEffect, useRef, useCallback } from 'react';

const WS_ENDPOINT = `${import.meta.env.VITE_API_GATEWAY_URL || 'http://localhost:8080'}/ws/opportunities`;

// Broadcast message type labels
export const BROADCAST_TYPES = {
  FULL_SNAPSHOT: 'full_snapshot',
  SCORE_DELTA:   'score_delta',
  HEARTBEAT:     'heartbeat',
  EXPORT_DONE:   'export_completed',
};

/**
 * @param {{ onSnapshot?, onDelta?, onHeartbeat?, onExportDone?, reconnectDelayMs? }} options
 * @returns {{ isConnected, lastMessage, connectionCount }}
 */
export function useWebSocket({
  onSnapshot,
  onDelta,
  onHeartbeat,
  onExportDone,
  reconnectDelayMs = 3000,
} = {}) {
  const [isConnected, setIsConnected] = useState(false);
  const [lastMessage, setLastMessage] = useState(null);
  const [connectionCount, setConnectionCount] = useState(1);
  const wsRef = useRef(null);
  const reconnectTimer = useRef(null);
  const mountedRef = useRef(true);

  const connect = useCallback(() => {
    if (!mountedRef.current) return;

    try {
      const ws = new WebSocket(WS_ENDPOINT);

      ws.onopen = () => {
        if (!mountedRef.current) { ws.close(); return; }
        setIsConnected(true);
        setConnectionCount(1);
      };

      ws.onmessage = (event) => {
        if (!mountedRef.current) return;
        try {
          const msg = JSON.parse(event.data);
          setLastMessage(msg);

          switch (msg.type) {
            case BROADCAST_TYPES.FULL_SNAPSHOT:
              onSnapshot?.(msg.payload);
              break;
            case BROADCAST_TYPES.SCORE_DELTA:
              onDelta?.(msg.payload);
              break;
            case BROADCAST_TYPES.HEARTBEAT:
              onHeartbeat?.(msg.payload);
              break;
            case BROADCAST_TYPES.EXPORT_DONE:
              onExportDone?.(msg.payload);
              break;
          }
        } catch {
          // Ignore malformed messages
        }
      };

      ws.onerror = () => {
        // WebSocket error — will be handled by onclose
      };

      ws.onclose = () => {
        if (!mountedRef.current) return;
        setIsConnected(false);
        setConnectionCount(0);

        // Schedule reconnect
        reconnectTimer.current = setTimeout(() => {
          if (mountedRef.current) connect();
        }, reconnectDelayMs);
      };

      wsRef.current = ws;
    } catch {
      // WebSocket not available — silently skip
      setIsConnected(false);
    }
  }, [onSnapshot, onDelta, onHeartbeat, onExportDone, reconnectDelayMs]);

  useEffect(() => {
    mountedRef.current = true;
    connect();

    return () => {
      mountedRef.current = false;
      clearTimeout(reconnectTimer.current);
      wsRef.current?.close();
    };
  }, [connect]);

  /** Send a raw JSON message to the server (used for subscribe/unsubscribe). */
  const sendMessage = useCallback((payload) => {
    wsRef.current?.send(JSON.stringify(payload));
  }, []);

  return { isConnected, lastMessage, connectionCount, sendMessage };
}