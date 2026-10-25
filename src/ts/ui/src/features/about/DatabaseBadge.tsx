// State of the local database, as a coloured badge.
import { Badge } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import { DatabaseStatus } from '@voyager/grpc-client';

export function DatabaseBadge({ status }: { readonly status: DatabaseStatus }) {
  const { l10n } = useLocalization();
  switch (status) {
    case DatabaseStatus.AVAILABLE:
      return <Badge color="success">{l10n.getString('about-database-available')}</Badge>;
    case DatabaseStatus.UNAVAILABLE:
      return <Badge color="danger">{l10n.getString('about-database-unavailable')}</Badge>;
    default:
      return <Badge color="informative">{l10n.getString('about-database-unknown')}</Badge>;
  }
}
