export const TRIAGE_COLOR = {
  ROT: 'rot',
  GELB: 'gelb',
  GRUEN: 'grün',
  BLAU: 'blau',
  SCHWARZ: 'schwarz',
} as const;

export const TRIAGE_COLORS = Object.values(TRIAGE_COLOR);

export type TriageColor = typeof TRIAGE_COLORS[number];

export const TRIAGE_COLOR_BACKGROUND: Record<TriageColor, string> = {
  rot: 'red',
  gelb: 'yellow',
  grün: 'green',
  blau: 'blue',
  schwarz: 'black',
};

export function isTriageColor(value: string | null | undefined): value is TriageColor {
  return value != null && (TRIAGE_COLORS as readonly string[]).includes(value);
}
