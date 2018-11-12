$().ready(function() {
    loadTreeFilter();
    loadEditable();
    loadSwitcher();
    loadTreePopup();
    loadOdds();
    disableBoxes();
});

function disableBoxes() {
    $('input[inactive="1"]').attr('disabled', 'disabled');
}

var filterDataLink = "/Master/Catalog/GetTreeData";
var saveFieldDataLink = "/Master/Catalog/SaveField";
var popupSaveLink = "/Master/Import/SaveSetting";


function loadOdds() {
    $('.odd-grid tr:odd').addClass('odd');
}

function loadTreePopup() {
    $('.del-btn').click(function () {
        if (confirm("Вы уверены что хотите удалить эту запись?")) {
            var btn = $(this);
            $.post(popupSaveLink, { target: $(this).attr('arg'), category: 0, action: "delete" }, function(d) {
                if (d.length) {
                    btn.parent().parent().remove();
                }
            });
        }
        return false;

    });
    $('#popup input[type="submit"]').click(function () {
        $.post(popupSaveLink, { target: $('#TargetID').val(), category: $('#PageListPlain').val().replace(';',''), action: "save" }, function(d) {
            if (d.length) {
                $("#popup").dialog('close');
                $('.popup-btn[arg="' + $('#TargetID').val() + '"]').html(d);
                
            }
        });
        return false;
    });
    $('.popup-btn').click(function() {
        $("#popup").dialog({
            autoOpen: true,
            show: "blind",
            hide: "explode",
            modal: true,
            width: 600,
            height: 600,
            resizable:false
                
        });
        $('#TargetID').val($(this).attr('arg'));
        return false;
    });
}


function loadSwitcher() {
    $('.switcher').click(function () {
        $('.switcher-content').toggle();
        var currentSwitch = $.cookie('switcher');
        if (currentSwitch == null)
            currentSwitch = '1';

        if (currentSwitch == '0')
            currentSwitch = '1';
        else currentSwitch = '0';
        $.cookie('switcher', currentSwitch);
        return false;
    });
    
    var currentSwitch = $.cookie('switcher');
    if (currentSwitch == null)
        currentSwitch = '1';

    if (currentSwitch == '0')
        $('.switcher-content').hide();
    else $('.switcher-content').show();
    
}

function loadEditable() {
    $('.editable').click(function () {
        if ($(this).hasClass('editing')) return false;
        $('.editable').filter('.editing').each(function() {
            $(this).html($(this).attr('val'));
            $(this).removeClass('editing');
        });
        $(this).html('<div class="cell"><input type="text" value="' + $(this).attr('val') + '"></div><div class="btns"><a class="accept" href="/"/><a class="cancel" href="/"></a></div>');
        $(this).addClass('editing');
        $('.editing .cancel').click(function () {
            var cell = $(this).parents('.editing');
            cell.html(cell.attr('val'));
            cell.removeClass('editing');
            return false;
        });
        $('.editing .accept').click(function () {
            var cell = $(this).parents('.editing');
            $.post(saveFieldDataLink, { field: cell.attr('target'), id: cell.attr('targetId'), value:cell.find('input').val() }, function(d) {
                if(d.length) {
                    cell.attr('val', d);
                    cell.html(cell.attr('val'));
                    cell.removeClass('editing');
                }
            });
            return false;
        });
    });
}

function setAnnotattionDemand() {
    var op = 1;
    if ($('#ByAnnotation').is(":checked")) {
        op = 0.5;
    }
    $('#search-form input[id!="ByAnnotation"]').filter('[type="checkbox"]').each(function () {
        $(this).parent().fadeTo(100, op);
        if (op < 1)
            $(this).attr('disabled', 'disabled');
        else $(this).removeAttr('disabled');
    });

}

var changing = false;
function loadTreeFilter() {
    try {

        $('#ByAnnotation').change(function() {
            setAnnotattionDemand();
        });
        setAnnotattionDemand();
        if ($('#tree-filter').length) {
            var argList = $('#PageListPlain').val().split(';');
            $.getJSON(filterDataLink, {}, function(res) {


                $('#tree-filter').jstree({
                    "plugins": [
                        "themes", "json_data", "ui", "cookies", "dnd", "search", "types", "checkbox"
                    ],

                    "cookies": {
                        "save_opened": "js_tree_catalog_filter",
                        "cookie_options": { expires: 365 }
                    },

                    "checkbox": {
                        "two_state": true
                    },

                    "themes": {
                        "theme": "apple",
                        "url": "/Content/themes/apple/style.css"
                    },

                    "json_data": {
                        "data": res,
                        "progressive_render": true
                    }
                }).bind("change_state.jstree", function(e, d) {
                    if (changing) return false;
                    var single = false;
                    try {
                        single = singleSelection;
                    } catch(e) {
                    }
                    var sections = $('#tree-filter').jstree("get_checked", null, true);

                    if (single) {
                        //console.log(d);
                        changing = true;
                        var current = d.rslt[0];
                        $('#tree-filter').jstree('uncheck_all');
                        $('#tree-filter').jstree('check_node', current);
                        changing = false;
                        sections = $('#tree-filter').jstree("get_checked", null, true);
                        //console.log(e);

                    }

                    var sectionPlain = '';
                    sections.each(function() {
                        sectionPlain += $(this).attr('id').replace('x', '') + ";";
                    });
                    $('#PageListPlain').val(sectionPlain);
                }).bind("loaded.jstree", function(event, data) {

                    for (var i = 0; i < argList.length; i++) {
                        $('#tree-filter').jstree("check_node", 'x' + argList[i]);
                    }

                }).bind("open_node.jstree", function(e, data) {
                    for (var i = 0; i < argList.length; i++) {
                        $('#tree-filter').jstree("check_node", '#x' + argList[i]);
                    }
                });
            });
        }
    }
    catch (exc) {

    }
}