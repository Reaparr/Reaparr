import {
	Dark,
	type ComponentConstructor,
	QCircularProgress,
	QLinearProgress,
	QSelect,
	QToggle,
	QExpansionItem,
	QMarkupTable,
	QTable,
	QInput,
	QChip,
	QAvatar,
} from 'quasar';
import { defineNuxtPlugin } from '#imports';

export default defineNuxtPlugin(() => {
	// Reaparr is dark-only, including calls to Dark.toggle() and Dark.set('auto').
	Dark.set = Dark.set.bind(Dark, true);
	Dark.set(true);
	setQuasarComponentDefaultPropValues(QInput, {
		outlined: true,
	});

	setQuasarComponentDefaultPropValues(QSelect, {
		outlined: true,
		dense: true,
		dropdownIcon: 'mdi-chevron-down',
		clearIcon: 'mdi-close',
	});

	setQuasarComponentDefaultPropValues(QToggle, {
		color: 'red',
		checkedIcon: 'mdi-check',
		uncheckedIcon: 'mdi-close',
	});

	setQuasarComponentDefaultPropValues(QMarkupTable, {
		flat: true,
	});

	setQuasarComponentDefaultPropValues(QTable, {
		color: 'red',
	});

	setQuasarComponentDefaultPropValues(QChip, {
		color: 'red',
		textColor: 'white',
	});

	setQuasarComponentDefaultPropValues(QAvatar, {
		color: 'red',
		textColor: 'white',
	});

	setQuasarComponentDefaultPropValues(QExpansionItem, {
		expandIcon: 'mdi-chevron-down',
		expandedIcon: 'mdi-chevron-up',
	});

	setQuasarComponentDefaultPropValues(QCircularProgress, {
		color: 'red',
		indeterminate: true,
	});

	setQuasarComponentDefaultPropValues(QLinearProgress, {
		color: 'red',
		trackColor: 'red',
		stripe: true,
		size: '20px',
	});
});

// Source: https://github.com/quasarframework/quasar/discussions/8761#discussioncomment-1042529
type ExtractComponentProps<T> = T extends ComponentConstructor<infer X> ? X['$props'] : never;
export const setQuasarComponentDefaultPropValues = <T extends ComponentConstructor>(
	component: T,
	propDefaults: {
		[K in keyof Partial<ExtractComponentProps<T>>]: ExtractComponentProps<T>[K];
	},
) => {
	for (const key in propDefaults) {
		const prop = component.props[key];
		switch (typeof prop) {
			case 'object':
				prop.default = propDefaults[key];
				break;
			case 'function':
				component.props[key] = {
					type: prop,
					default: propDefaults[key],
				};
				break;
			case 'undefined':
				throw new Error('unknown prop: ' + key);
			default:
				throw new Error('unhandled type: ' + typeof prop);
		}
	}
};
