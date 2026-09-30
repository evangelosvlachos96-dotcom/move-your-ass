/** The networks the editor offers, in the order its dropdown lists them. Mirrors SiteRules. */
export const NETWORKS = ['instagram', 'youtube', 'tiktok', 'facebook', 'whatsapp', 'website'] as const;

export type Network = (typeof NETWORKS)[number];

/** WhatsApp is a phone number, not a URL; everything else is a link. */
export const WHATSAPP: Network = 'whatsapp';

/** One network the trainer added, with the value she gave it. Order is hers. */
export interface SocialLink {
  network: Network;
  value: string;
}

/** What the About page shows for each network: its label, and how its value is asked for. */
export const NETWORK_INFO: Record<Network, { label: string; placeholder: string; hint?: string }> = {
  instagram: { label: 'Instagram', placeholder: 'https://instagram.com/…' },
  youtube: { label: 'YouTube', placeholder: 'https://youtube.com/…' },
  tiktok: { label: 'TikTok', placeholder: 'https://tiktok.com/@…' },
  facebook: { label: 'Facebook', placeholder: 'https://facebook.com/…' },
  whatsapp: {
    label: 'WhatsApp',
    placeholder: '3069XXXXXXXX',
    hint: 'Μόνο αριθμοί, με τον κωδικό χώρας μπροστά.',
  },
  website: { label: 'Ιστοσελίδα', placeholder: 'https://…' },
};

/** The About page as the API returns it. Every field is optional; empty ones are hidden. */
export interface About {
  photoUrl: string | null;
  trainerName: string | null;
  tagline: string | null;
  aboutMarkdown: string | null;
  contactEmail: string | null;
  phone: string | null;
  /** The trainer's scheduling page. Null hides every booking button in the app. */
  bookingUrl: string | null;
  socialLinks: SocialLink[];
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
