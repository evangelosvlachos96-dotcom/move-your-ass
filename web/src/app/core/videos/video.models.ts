export type Audience = 'Male' | 'Female' | 'Both';
export type BodyArea = 'FullBody' | 'UpperBody' | 'LowerBody';
export type VideoStatus = 'Uploading' | 'Processing' | 'Ready' | 'Failed' | 'Deleting';
export interface Tag {
  id: string;
  name: string;
  usageCount: number;
}
export interface Video {
  id: string;
  title: string;
  description: string | null;
  audience: Audience;
  bodyArea: BodyArea;
  requiresEquipment: boolean;
  status: VideoStatus;
  isPublished: boolean;
  sortOrder: number;
  durationSeconds: number | null;
  thumbnailUrl: string | null;
  revision: string;
  tags: Tag[];
}
export interface VideoInput {
  title: string;
  description: string | null;
  audience: Audience;
  bodyArea: BodyArea;
  requiresEquipment: boolean;
  tagIds: string[];
  revision?: string;
}
export interface VideoPage {
  items: Video[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
export interface UploadCredentials {
  endpoint: string;
  videoId: string;
  libraryId: string;
  signature: string;
  expires: number;
}
export interface CreatedVideo {
  id: string;
  upload: UploadCredentials | null;
}
export interface Playback {
  url: string;
  expires: number;
}
export interface VideoSummary {
  total: number;
  published: number;
  processing: number;
  failed: number;
  providerConfigured: boolean;
}
export const AUDIENCE_LABELS: Record<Audience, string> = {
  Male: 'Άντρες',
  Female: 'Γυναίκες',
  Both: 'Όλοι',
};
export const AREA_LABELS: Record<BodyArea, string> = {
  FullBody: 'Όλο το σώμα',
  UpperBody: 'Πάνω μέρος',
  LowerBody: 'Κάτω μέρος',
};
export const STATUS_LABELS: Record<VideoStatus, string> = {
  Uploading: 'Αναμονή ανεβάσματος',
  Processing: 'Επεξεργασία',
  Ready: 'Έτοιμο',
  Failed: 'Αποτυχία',
  Deleting: 'Διαγραφή σε εκκρεμότητα',
};
export function durationLabel(seconds: number | null): string {
  if (seconds === null) return '—';
  return Math.floor(seconds / 60) + ':' + String(seconds % 60).padStart(2, '0');
}
