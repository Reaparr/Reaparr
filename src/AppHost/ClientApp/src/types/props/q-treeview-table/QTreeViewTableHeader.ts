import type { SortDirection, SortType } from '@dto';

export interface QTreeViewTableHeader {
	label: string;
	field: string;
	sortField?: SortType;
	width?: number;
	type?: 'title' | 'duration' | 'file-size' | 'file-speed' | 'date' | 'actions' | 'datetime' | 'percentage' | 'index' | 'media-quality';
	sortable?: boolean;
	required?: boolean;
	sortOrder?: SortDirection;
	align?: 'left' | 'center' | 'right';
	headerStyle?: string;
}
