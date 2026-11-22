declare module 'markdown-it' {
	export type Options = Record<string, unknown>;

	export interface Token {
		attrs: [string, string][] | null;
		attrIndex(name: string): number;
		attrPush(attribute: [string, string]): void;
	}

	export interface Renderer {
		rules: {
			link_open?: RendererRule;
			[name: string]: RendererRule | undefined;
		};
		renderToken(tokens: Token[], index: number, options: Options): string;
	}

	export type RendererRule = (
		tokens: Token[],
		index: number,
		options: Options,
		env: unknown,
		renderer: Renderer,
	) => string;

	export type PluginSimple = (markdownIt: MarkdownIt) => void;

	class MarkdownIt {
		constructor(options?: Options);
		renderer: Renderer;
		use(plugin: PluginSimple): this;
		render(source: string): string;
	}

	export default MarkdownIt;
}
