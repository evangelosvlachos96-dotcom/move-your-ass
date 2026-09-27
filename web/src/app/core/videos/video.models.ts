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
  sizeBytes: number | null;
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
export interface PartUrl {
  partNumber: number;
  url: string;
}
/** Everything the browser needs to upload directly to object storage. No provider credential. */
export interface UploadTicket {
  partSizeBytes: number;
  partCount: number;
  parts: PartUrl[];
  /** Parts the provider already holds, so reopening a draft resumes rather than restarts. */
  uploadedParts: number[];
  thumbnailUploadUrl: string;
}
export interface UploadRequest {
  contentType: string;
  sizeBytes: number;
}
export interface CreatedVideo {
  id: string;
  upload: UploadTicket | null;
}
export interface StorageUsage {
  usedBytes: number;
  capBytes: number;
  maxFileBytes: number;
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
  storage: StorageUsage;
}

/** Content types the API accepts. Anything else is refused before an upload URL is issued. */
export const ACCEPTED_VIDEO_TYPES = ['video/mp4', 'video/quicktime'] as const;
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
  Uploading: 'Ημιτελές ανέβασμα',
  Processing: 'Επεξεργασία',
  Ready: 'Έτοιμο',
  Failed: 'Αποτυχία',
  Deleting: 'Διαγραφή σε εκκρεμότητα',
};
export function durationLabel(seconds: number | null): string {
  if (seconds === null) return '—';
  return Math.floor(seconds / 60) + ':' + String(seconds % 60).padStart(2, '0');
}

/** Human-readable size, for the storage bar and for file-too-large messages. */
export function sizeLabel(bytes: number | null): string {
  if (bytes === null) return '—';
  if (bytes < 1024 ** 2) return Math.round(bytes / 1024) + ' KB';
  if (bytes < 1024 ** 3) return (bytes / 1024 ** 2).toFixed(bytes < 10 * 1024 ** 2 ? 1 : 0) + ' MB';
  return (bytes / 1024 ** 3).toFixed(2) + ' GB';
}
