/**
 * The hand-drawn icon set, ported path-for-path from BrandX's
 * `fragments/layout.html`.
 *
 * One outline style throughout (24x24, ~1.75 stroke, round caps and joins,
 * `currentColor`) so every icon takes its colour from context: plain in nav links and
 * buttons, or wrapped in a tinted `IconBadge` beside page titles and dashboard tiles.
 * Hand-written rather than an icon package, consistent with this app having no
 * external asset dependencies.
 */

type IconProps = { className?: string };

const base = {
  className: 'icon',
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
} as const;

export function IconProduct({ className = 'icon' }: IconProps) {
  return (
    <svg {...base} className={className} strokeWidth={1.75}>
      <path d="M12 3 4 7.5v9L12 21l8-4.5v-9L12 3Z" />
      <path d="M4 7.5 12 12l8-4.5" />
      <path d="M12 12v9" />
    </svg>
  );
}

export function IconOrder({ className = 'icon' }: IconProps) {
  return (
    <svg {...base} className={className} strokeWidth={1.75}>
      <rect x="9" y="2.5" width="6" height="3" rx="1" />
      <rect x="5" y="4" width="14" height="17" rx="2" />
      <path d="m9 12.5 2 2 4-4.5" />
    </svg>
  );
}

export function IconCustomer({ className = 'icon' }: IconProps) {
  return (
    <svg {...base} className={className} strokeWidth={1.75}>
      <circle cx="12" cy="8" r="3.5" />
      <path d="M5 20c0-4.1 3.1-7 7-7s7 2.9 7 7" />
    </svg>
  );
}

export function IconPlus({ className = 'icon' }: IconProps) {
  return (
    <svg {...base} className={className} strokeWidth={1.9}>
      <path d="M12 5v14M5 12h14" />
    </svg>
  );
}

export function IconEdit({ className = 'icon' }: IconProps) {
  return (
    <svg {...base} className={className} strokeWidth={1.75}>
      <path d="M14.5 4.5a2.1 2.1 0 0 1 3 3L7 18l-4 1 1-4Z" />
      <path d="M13 6l3 3" />
    </svg>
  );
}

export function IconTrash({ className = 'icon' }: IconProps) {
  return (
    <svg {...base} className={className} strokeWidth={1.75}>
      <path d="M4 7h16" />
      <path d="M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2" />
      <path d="M18.5 7 17.6 19a2 2 0 0 1-2 1.8H8.4a2 2 0 0 1-2-1.8L5.5 7Z" />
      <path d="M10 11v6M14 11v6" />
    </svg>
  );
}

export function IconCheck({ className = 'icon' }: IconProps) {
  return (
    <svg {...base} className={className} strokeWidth={1.9}>
      <path d="M4 12.5 9.5 18 20 6" />
    </svg>
  );
}

export function IconSearch({ className = 'icon' }: IconProps) {
  return (
    <svg {...base} className={className} strokeWidth={1.9}>
      <circle cx="10.5" cy="10.5" r="6.5" />
      <path d="m20 20-4.8-4.8" />
    </svg>
  );
}

export type Entity = 'product' | 'order' | 'customer';

const GLYPHS = {
  product: IconProduct,
  order: IconOrder,
  customer: IconCustomer,
};

/**
 * The same icon in the colour-coded square each entity keeps everywhere it appears.
 * Nav links are the one exception -- a pastel badge would wash out against the dark
 * top bar, so nav icons stay plain and inherit the link's own colour.
 */
export function IconBadge({ entity }: { entity: Entity }) {
  const Glyph = GLYPHS[entity];
  return (
    <span className={`icon-badge icon-badge-${entity}`}>
      <Glyph />
    </span>
  );
}
