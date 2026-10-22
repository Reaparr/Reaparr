import type { SortDirection, SortType } from '@dto';

export interface ISortOption {
	field: SortType;
	label: string;
	direction: SortDirection;
}
