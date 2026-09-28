/** The About page as the API returns it. Every field is optional; empty ones are hidden. */
export interface About {
  photoUrl: string | null;
  trainerName: string | null;
  tagline: string | null;
  aboutMarkdown: string | null;
  contactEmail: string | null;
  phone: string | null;
  instagram: string | null;
  youTube: string | null;
  tikTok: string | null;
  facebook: string | null;
  whatsApp: string | null;
  website: string | null;
  revision: string;
}

/** What an admin sends back. Mirrors {@link About} minus the photo, which has its own endpoints. */
export type AboutInput = Omit<About, 'photoUrl'>;

export interface ContactInput {
  subject: string;
  message: string;
}

export interface PhotoTicket {
  objectKey: string;
  uploadUrl: string;
}

/** Field limits, mirroring the server's validators so the form can say so before submitting. */
export const ABOUT_LIMITS = {
  trainerName: 120,
  tagline: 200,
  aboutMarkdown: 4000,
  contactEmail: 256,
  phone: 40,
  link: 200,
  whatsApp: 20,
  subject: 150,
  message: 4000,
} as const;

export const CONTACT_MIN = { subject: 3, message: 10 } as const;
