import React, { ReactElement } from 'react';
import Link from 'Components/Link/Link';

interface InlineMarkdownProps {
  className?: string;
  data?: string;
  blockClassName?: string;
}

function InlineMarkdown(props: InlineMarkdownProps) {
  const { className, data, blockClassName } = props;
  const markdownBlocks: (ReactElement | string)[] = [];

  if (data) {
    const markdownRegex = /\[([^\]]+?)\]\(([^)]+?)\)|`([^`]+?)`/g;

    let endIndex = 0;
    let match: RegExpExecArray | null = null;

    while ((match = markdownRegex.exec(data)) !== null) {
      if (match.index > endIndex) {
        markdownBlocks.push(data.slice(endIndex, match.index));
      }

      if (match[1] && match[2]) {
        markdownBlocks.push(
          <Link key={`link-${match.index}`} to={match[2]}>
            {match[1]}
          </Link>
        );
      } else if (match[3]) {
        markdownBlocks.push(
          <code
            key={`code-${match.index}`}
            className={blockClassName ?? undefined}
          >
            {match[3]}
          </code>
        );
      }
      endIndex = match.index + match[0].length;
    }

    if (markdownBlocks.length > 0 && endIndex < data.length) {
      markdownBlocks.push(data.slice(endIndex));
    }

    if (markdownBlocks.length === 0) {
      markdownBlocks.push(data);
    }
  }

  return <span className={className}>{markdownBlocks}</span>;
}

export default InlineMarkdown;
