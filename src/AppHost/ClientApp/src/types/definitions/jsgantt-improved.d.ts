declare module 'jsgantt-improved' {
	export type GanttChartFormat = 'hour' | 'day' | 'week' | 'month' | 'quarter';

	export interface GanttTaskInput extends Record<string, unknown> {
		pID: string | number;
		pName: string;
		pStart: Date;
		pEnd: Date;
		pClass: string;
		pLink: string;
		pMile: number;
		pRes: string;
		pComp: number;
		pGroup: number;
		pParent: string | number;
		pOpen: number;
		pDepend: string;
		pCaption: string;
		pNotes: string;
	}

	export interface GanttTask {
		getOriginalID(): string;
		getListChildRow(): HTMLTableRowElement | null;
	}

	export interface GanttChartOptions {
		vCaptionType: string;
		vDayColWidth: number;
		vWeekColWidth: number;
		vMonthColWidth: number;
		vQuarterColWidth: number;
		vDateTaskDisplayFormat: string;
		vDayMajorDateDisplayFormat: string;
		vDayMinorDateDisplayFormat: string;
		vWeekMinorDateDisplayFormat: string;
		vLang: string;
		vShowTaskInfoLink: number;
		vShowSelector: string[];
		vShowRes: number;
		vShowDur: number;
		vShowComp: number;
		vShowStartDate: number;
		vShowEndDate: number;
		vShowTaskInfoComp: number;
		vShowTaskInfoStartDate: number;
		vShowTaskInfoEndDate: number;
		vShowEndWeekDate: number;
		vShowWeekends: number;
		vUseSingleCell: number;
		vFormatArr: GanttChartFormat[];
	}

	export class GanttChart {
		constructor(container: HTMLElement, format: GanttChartFormat);
		setOptions(options: GanttChartOptions): void;
		AddTaskItemObject(task: GanttTaskInput): void;
		setMinDate(date: Date): void;
		setMaxDate(date: Date): void;
		getList(): GanttTask[];
		Draw(): void;
	}

	const JSGantt: {
		GanttChart: typeof GanttChart;
	};

	export default JSGantt;
}
