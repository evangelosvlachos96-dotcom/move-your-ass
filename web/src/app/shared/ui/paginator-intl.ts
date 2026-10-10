import { MatPaginatorIntl } from '@angular/material/paginator';

export function greekPaginatorIntl(): MatPaginatorIntl {
  const intl = new MatPaginatorIntl();
  intl.itemsPerPageLabel = 'Ανά σελ.';
  intl.nextPageLabel = 'Επόμενη σελίδα';
  intl.previousPageLabel = 'Προηγούμενη σελίδα';
  intl.firstPageLabel = 'Πρώτη σελίδα';
  intl.lastPageLabel = 'Τελευταία σελίδα';
  intl.getRangeLabel = (page, size, length) => {
    if (!length || !size) return '0 / ' + length;
    const start = page * size;
    return start + 1 + '–' + Math.min(start + size, length) + ' / ' + length;
  };
  return intl;
}
