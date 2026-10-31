import type { QTreeViewTableHeader } from '@props';
import { useI18n } from '#build/imports';
import { SortDirection, SortType } from '@dto';

export const getMediaTableColumns = (): QTreeViewTableHeader[] => {
	const { t } = useI18n();

	return [
		{
			label: t('components.media-list.columns.title'),
			field: 'title',
			sortField: SortType.Title,
			type: 'title',
			align: 'left',
			sortOrder: SortDirection.Asc,
			sortable: true,
			required: true,
		},
		{
			label: t('components.media-list.columns.quality'),
			field: 'quality',
			sortField: SortType.VideoQuality,
			align: 'center',
			sortable: false,
		},
		{
			label: t('components.media-list.columns.comparison-state'),
			field: 'comparisonState',
			sortField: SortType.ComparisonState,
			align: 'center',
			sortable: true,
		},
		{
			label: t('components.media-list.columns.year'),
			field: 'year',
			sortField: SortType.Year,
			align: 'center',
			sortable: true,
		},
		{
			label: t('components.media-list.columns.duration'),
			field: 'duration',
			sortField: SortType.Duration,
			type: 'duration',
			align: 'center',
			sortable: true,
		},
		{
			label: t('components.media-list.columns.media-size'),
			field: 'mediaSize',
			sortField: SortType.MediaSize,
			type: 'file-size',
			align: 'center',
			sortable: true,
		},
		{
			label: t('components.media-list.columns.added-at'),
			field: 'addedAt',
			sortField: SortType.AddedAt,
			type: 'date',
			align: 'center',
			sortable: true,
		},
		{
			label: t('components.media-list.columns.updated-at'),
			field: 'updatedAt',
			sortField: SortType.UpdatedAt,
			type: 'date',
			align: 'center',
			sortable: true,
		},
		{
			label: t('components.media-list.columns.actions'),
			field: 'actions',
			align: 'center',
			type: 'actions',
			required: true,
		},
	];
};
