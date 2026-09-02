import type { CSSProperties, ReactNode } from 'react';

/**
 * Iconos de trazo en rejilla 24, un solo estilo en todo el sistema. Nunca emoji.
 * Se declaran como JSX y no como cadenas de HTML: así no hace falta
 * dangerouslySetInnerHTML en ninguna parte de la aplicación.
 */
const ICONS: Record<string, ReactNode> = {
  grid: <><path d="M3 3h7v7H3z" /><path d="M14 3h7v7h-7z" /><path d="M3 14h7v7H3z" /><path d="M14 14h7v7h-7z" /></>,
  box: <><path d="M21 8v8a2 2 0 0 1-1 1.7l-7 4a2 2 0 0 1-2 0l-7-4A2 2 0 0 1 3 16V8a2 2 0 0 1 1-1.7l7-4a2 2 0 0 1 2 0l7 4A2 2 0 0 1 21 8Z" /><path d="m3.3 7 8.7 5 8.7-5" /><path d="M12 22V12" /></>,
  swap: <><path d="M7 4v13" /><path d="m4 14 3 3 3-3" /><path d="M17 20V7" /><path d="m14 10 3-3 3 3" /></>,
  tag: <><path d="M12.6 3H6a3 3 0 0 0-3 3v6.6a2 2 0 0 0 .6 1.4l7.4 7.4a2 2 0 0 0 2.8 0l6.6-6.6a2 2 0 0 0 0-2.8L14 3.6A2 2 0 0 0 12.6 3Z" /><circle cx="8" cy="8" r="1.4" /></>,
  truck: <><path d="M2 7h11v9H2z" /><path d="M13 10h4l3 3v3h-7z" /><circle cx="6" cy="18.5" r="1.8" /><circle cx="17" cy="18.5" r="1.8" /></>,
  users: <><path d="M15 20v-1.5A3.5 3.5 0 0 0 11.5 15h-5A3.5 3.5 0 0 0 3 18.5V20" /><circle cx="9" cy="8" r="3.4" /><path d="M21 20v-1.5a3.5 3.5 0 0 0-2.6-3.4" /><path d="M15.6 4.6a3.4 3.4 0 0 1 0 6.6" /></>,
  chart: <><path d="M3 3v18h18" /><path d="M7 15v-4" /><path d="M12 17V7" /><path d="M17 17v-7" /></>,
  shield: <><path d="M12 22s8-3.6 8-10V5.4l-8-3.2-8 3.2V12c0 6.4 8 10 8 10Z" /><path d="m9 12 2 2 4-4" /></>,
  search: <><circle cx="10.5" cy="10.5" r="6.5" /><path d="m20 20-4.7-4.7" /></>,
  plus: <><path d="M12 5v14" /><path d="M5 12h14" /></>,
  pencil: <><path d="M4 20h4l10-10-4-4L4 16v4Z" /><path d="m14 6 4 4" /></>,
  trash: <><path d="M4 7h16" /><path d="M9 7V4h6v3" /><path d="M6 7l1 13h10l1-13" /></>,
  undo: <><path d="M4 10h9a5 5 0 0 1 0 10H7" /><path d="m8 6-4 4 4 4" /></>,
  lock: <><rect x="4.5" y="10.5" width="15" height="10" rx="2" /><path d="M8 10.5V7a4 4 0 0 1 8 0v3.5" /></>,
  alert: <><path d="M12 3 2.5 20h19L12 3Z" /><path d="M12 9v5" /><path d="M12 17.2h.01" /></>,
  info: <><circle cx="12" cy="12" r="9" /><path d="M12 11v6" /><path d="M12 8h.01" /></>,
  check: <path d="m4 13 5 5L20 7" />,
  x: <><path d="M6 6l12 12" /><path d="M18 6 6 18" /></>,
  left: <path d="m14 6-6 6 6 6" />,
  right: <path d="m10 6 6 6-6 6" />,
  down: <path d="m6 10 6 6 6-6" />,
  up: <path d="m6 14 6-6 6 6" />,
  filter: <><path d="M3 5h18" /><path d="M6 12h12" /><path d="M10 19h4" /></>,
  clock: <><circle cx="12" cy="12" r="9" /><path d="M12 7.5V12l3.5 2" /></>,
  logout: <><path d="M14 4h4a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-4" /><path d="M10 12h10" /><path d="m16 8 4 4-4 4" /></>,
  empty: <><rect x="3" y="6" width="18" height="14" rx="2" /><path d="M3 11h18" /><path d="M9 6V4h6v2" /></>,
  calendar: <><rect x="3" y="5" width="18" height="16" rx="2" /><path d="M3 10h18" /><path d="M8 3v4" /><path d="M16 3v4" /></>,
  sun: <><circle cx="12" cy="12" r="4.2" /><path d="M12 4V2" /><path d="M12 22v-2" /><path d="m5 5-1.4-1.4" /><path d="m20.4 20.4-1.4-1.4" /><path d="M4 12H2" /><path d="M22 12h-2" /><path d="m5 19-1.4 1.4" /><path d="m20.4 3.6-1.4 1.4" /></>,
  moon: <path d="M20 14.5A8.5 8.5 0 1 1 9.5 4a6.8 6.8 0 0 0 10.5 10.5Z" />,
  logo: <><path d="M12 2.6 20.5 7v10L12 21.4 3.5 17V7Z" /><path d="M12 12v9.4" /><path d="m3.5 7 8.5 5 8.5-5" /></>,
  spinner: <path d="M12 3a9 9 0 1 0 9 9" />,
};

export type IconName = keyof typeof ICONS;

interface Props {
  name: string;
  size?: number;
  strokeWidth?: number;
  style?: CSSProperties;
  spin?: boolean;
}

export function Icon({ name, size = 16, strokeWidth = 1.6, style, spin }: Props) {
  const content = ICONS[name];
  if (!content) return null;

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      style={{
        flexShrink: 0,
        display: 'block',
        ...(spin ? { animation: 'shx-spin .8s linear infinite' } : null),
        ...style,
      }}
    >
      {content}
    </svg>
  );
}
