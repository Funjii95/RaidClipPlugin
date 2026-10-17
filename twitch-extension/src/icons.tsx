import type { ReactNode } from "react";

type IconName =
  | "otter"
  | "compass"
  | "bag"
  | "swords"
  | "sets"
  | "perk"
  | "boss"
  | "cook"
  | "time"
  | "dive"
  | "fish"
  | "pearl";
const paths: Record<IconName, ReactNode> = {
  cook: (
    <>
      <path d="M4 11h16v4a6 6 0 0 1-6 6h-4a6 6 0 0 1-6-6ZM1 12h3m16 0h3M8 8C4 5 10 5 7 2m6 6c-4-3 2-3-1-6m6 6c-4-3 2-3-1-6" />
    </>
  ),
  otter: (
    <>
      <path d="M6 8 4 5Q2 6 4 10M18 8l2-3q2 1 0 5M5 10q7-8 14 0v5q-7 8-14 0Z" />
      <path d="m10 14 2 2 2-2m-2 2v2m-4-7h.01M16 11h.01" />
    </>
  ),
  compass: (
    <>
      <circle cx="12" cy="12" r="8" />
      <path d="M12 1v3m0 16v3M1 12h3m16 0h3M9 15l2-6 4 0-2 6Z" />
    </>
  ),
  bag: (
    <>
      <path d="M8 7V5q4-5 8 0v2M6 7h12l3 13H3ZM6 7l6 5 6-5M8 17h8m-4-5v3" />
    </>
  ),
  swords: (
    <>
      <path d="m4 3 2 6 11 11m0-4-4 4m4-5 4 4M20 3l-2 6L7 20m0-4 4 4M7 15l-4 4" />
    </>
  ),
  sets: (
    <>
      <path d="m3 6 9-4 9 4-9 4Zm0 6 9 4 9-4M3 18l9 4 9-4M3 6v4m18-4v4M3 12v4m18-4v4" />
    </>
  ),
  perk: (
    <>
      <path d="M12 22V9m0 4Q2 14 3 4q9 0 9 9Zm0 4q10 1 9-9-9 0-9 9Z" />
    </>
  ),
  boss: (
    <>
      <path d="M6 12q-4-3-2-8l5 4m9 4q4-3 2-8l-5 4M6 12q0-8 6-6 6-2 6 6v5l-3 1v4l-3-2-3 2v-4l-3-1Z" />
      <path d="m8 12 2 1m4 0 2-1m-4 3v2" />
    </>
  ),
  time: (
    <>
      <path d="M5 2h14M5 22h14M7 2v5l10 10v5M17 2v5L7 17v5M9 6h6m-6 13h6" />
    </>
  ),
  dive: (
    <>
      <path d="M2 13q5-7 10-1t10-1M2 18q5-7 10-1t10-1M12 2v6m-3-3 3 3 3-3" />
    </>
  ),
  fish: (
    <>
      <path d="M15 2v14a5 5 0 1 1-10 0v-4l3 3M12 2h6" />
      <circle cx="15" cy="3" r="1" />
    </>
  ),
  pearl: (
    <>
      <circle cx="12" cy="12" r="8" />
      <path d="M7 10q0-3 4-4" />
    </>
  ),
};
export function Icon({
  name,
  className = "",
}: {
  name: IconName;
  className?: string;
}) {
  return (
    <svg
      className={`ui-icon ${className}`}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.45"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {paths[name]}
    </svg>
  );
}
