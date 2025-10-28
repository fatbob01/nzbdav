import type { Route } from "./+types/route";
import { Layout } from "../_index/components/layout/layout";
import { TopNavigation } from "../_index/components/top-navigation/top-navigation";
import { LeftNavigation } from "../_index/components/left-navigation/left-navigation";


export default function QuickStream(props: Route.ComponentProps) {
    return (
        <Layout
            topNavComponent={TopNavigation}
            bodyChild={<Body />}
            leftNavChild={<LeftNavigation />}
        />
    );
}

function Body() {
    return (
        <div>QUICK STREAM</div>
    );
}
