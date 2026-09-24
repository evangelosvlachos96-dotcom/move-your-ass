import { Pipe, PipeTransform } from '@angular/core';

const ATHENS = 'Europe/Athens';

const dateFormat = new Intl.DateTimeFormat('el-GR', { dateStyle: 'medium', timeZone: ATHENS });
const dateTimeFormat = new Intl.DateTimeFormat('el-GR', { dateStyle: 'medium', timeStyle: 'short', timeZone: ATHENS });

/**
 * UTC on the wire, Europe/Athens on screen (CLAUDE.md rule 3). The API serialises DateTime
 * without a zone designator when EF hands back Kind=Unspecified, which JavaScript would read as
 * local time; a bare timestamp is therefore pinned to UTC before conversion.
 */
@Pipe({ name: 'athensDate' })
export class AthensDatePipe implements PipeTransform {
  transform(value: string | Date | null | undefined, style: 'date' | 'dateTime' = 'date'): string {
    if (!value) {
      return '';
    }
    const date = typeof value === 'string' ? new Date(pinToUtc(value)) : value;
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    return (style === 'dateTime' ? dateTimeFormat : dateFormat).format(date);
  }
}

function pinToUtc(iso: string): string {
  return /(?:Z|[+-]\d{2}:?\d{2})$/i.test(iso) ? iso : `${iso}Z`;
}
