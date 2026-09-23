/**
 * Display formatting, matching what the Thymeleaf templates produced so the two
 * applications can be compared screen for screen.
 */

/** `#numbers.formatDecimal(x, 1, 'COMMA', 2, 'POINT')` -- e.g. 1,234.56 */
export function money(value: number): string {
  return value.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/**
 * `d MMM yyyy, HH:mm` -- e.g. 22 Sep 2026, 15:56.
 *
 * The server sends a local date-time with no zone (the Java side stored
 * LocalDateTime), which is exactly how the browser parses an ISO string without a
 * trailing Z, so no conversion happens on the way in.
 */
export function dateTime(value: string | null | undefined): string {
  if (!value) return '—';
  const at = new Date(value);
  if (Number.isNaN(at.getTime())) return '—';

  const hours = String(at.getHours()).padStart(2, '0');
  const minutes = String(at.getMinutes()).padStart(2, '0');
  return `${at.getDate()} ${MONTHS[at.getMonth()]} ${at.getFullYear()}, ${hours}:${minutes}`;
}
