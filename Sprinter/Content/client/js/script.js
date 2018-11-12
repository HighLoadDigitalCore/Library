$(document).ready(function ($) {
    loadBlurs();
    loadVerticalMenu();
    loadSubmit();
    loadSwitcher();
    loadMarks();
    loadCounters();
    loadBuyBtns();
    loadShopCart();
    loadOrderTabs();
    loadAuthBtn();
    loadAutoSavers();
    loadProviderRadio();
    loadBookTabSwitchers();
    loadOneClickBuy();
});


function loadOneClickBuy(target) {
    var btns = $('.buyitnow');
    if (target != null)
        btns = target.find('.buyitnow');
    btns.click(function () {
        $.post('/Master/Shopcart/toCartImmediate', { id: $(this).attr('arg') }, function (data) {
            document.location.href = data;

        });
        return false;
    });
}

function loadBookTabSwitchers() {
    $('.detailed a[arg="book-tab-switcher"]').each(function () {
        if ($.trim($('#detailed-' + $(this).parent().attr('id')).html()) == '') {
            $(this).parent().hide();
        }
    });
    var currentTab = $.cookie('product-tab');
    if (!currentTab || !currentTab.length)
        currentTab = 'annotation';

    $('#detailed-' + currentTab).show();
    $('#' + currentTab).append('<span class="arrow_db"></span>');
    $('#' + currentTab).attr('class', 'act_d');


    //$.cookie(savename, a.attr('num'), { expires: 7, path: '/' });

    $('.detailed a[arg="book-tab-switcher"]').click(function () {
        $.cookie('product-tab', $(this).parent().attr('id'), { expires: 7, path: '/' });
        $('.detailed .tab').hide();
        $('.detailed .arrow_db').remove();
        $('.detailed ul li').attr('class', 'noact');
        $('#detailed-' + $(this).parent().attr('id')).show();
        $(this).parent().append('<span class="arrow_db"></span>');
        $(this).parent().attr('class', 'act_d');
        return false;
    });
}

function loadBlurs() {
    $('.blur-box').focus(function () {
        if ($(this).val() == $(this).attr('def'))
            $(this).val('');
    }).blur(function () {
        if ($.trim($(this).val()) == '')
            $(this).val($(this).attr('def'));

    });
    $('.blur-box[req="1"]').parents('form').find('input[type="submit"]').click(function () {
        var reqs = $(this).parents('form').find('input[req="1"]');
        var error = false;
        reqs.each(function () {
            if ($.trim($(this).val()) == '' || $.trim($(this).val()) == $(this).attr('def'))
                error = true;
        });
        return !error;
    });
}

function loadProviderRadio() {
    if ($('.personal-information').length) {

        $('#orgSwitcher').click(function () {
            //OrgType
            var items = new Array();
            items.push({ name: "ShowOrg", value: $(this).attr('tval') });


            $.ajax({
                url: '/Master/ShopCart/autoSave',
                data: $.toJSON(items),
                type: 'POST',
                contentType: 'application/json',
                success: function (data) {
                    $.post('/Master/ShopCart/Index', { step: 3 }, function (d) {
                        $('.main').replaceWith(d);
                        loadAuthBtn();
                        loadAutoSavers();
                        loadProviderRadio();
                    });
                },
                dataType: "html"
            });

            return false;
        });

        $('#DeliveryRegion').change(function () {
            var items = new Array();
            items.push({ name: $(this).attr('id'), value: $(this).val() });


            $.ajax({
                url: '/Master/ShopCart/autoSave',
                data: $.toJSON(items),
                type: 'POST',
                contentType: 'application/json',
                success: function (data) {
                    $.post('/Master/ShopCart/Index', { step: 3 }, function (d) {
                        $('.main').replaceWith(d);
                        loadAuthBtn();
                        loadAutoSavers();
                        loadProviderRadio();
                    });
                },
                dataType: "html"
            });
        });

    }

    $('input[name="AuthType"]').change(function () {
        var items = new Array();
        items.push({ name: $(this).attr('id'), value: $(this).val() });


        $.ajax({
            url: '/Master/ShopCart/autoSave',
            data: $.toJSON(items),
            type: 'POST',
            contentType: 'application/json',
            success: function (data) {
                $.get('/Master/ShopCart/Index', { step: 1 }, function (d) {
                    $('.main').replaceWith(d);
                    loadAuthBtn();
                    loadAutoSavers();
                    loadProviderRadio();
                });
            },
            dataType: "html"
        });
    });
    if ($('.delivery').length) {
        $('.content-block input[type="radio"], .content-block select').change(function () {
            var items = new Array();
            $('.content-block input[type="radio"]:checked, .content-block select').each(function () {
                items.push({ name: $(this).attr('id'), value: $(this).val() });
            });


            $.ajax({
                url: '/Master/ShopCart/autoSave',
                data: $.toJSON(items),
                type: 'POST',
                contentType: 'application/json',
                success: function (data) {
                    $.post('/Master/ShopCart/Index', { step: 2 }, function (d) {
                        $('.main').replaceWith(d);
                        loadAuthBtn();
                        loadAutoSavers();
                        loadProviderRadio();
                    });
                },
                dataType: "html"
            });
        });
    }
}

function loadAuthBtn() {


    $('.make-out').attr('rel', $('.tab-bottom .right').attr('rel'));
    $('.make-out').attr('arg', $('.tab-bottom .right').attr('arg'));
    $('.make-out').attr('fields', $('.tab-bottom .right').attr('fields'));

    $('a[arg="auth"]').click(function () {
        var ref = $(this).attr('href');
        $.post('/Master/ShopCart/check', { name: $('#UserMail').val(), pass: $('#UserPass').val(), type: $('#AuthType:checked').val() }, function (data) {
            if (data.length) {
                $('#messageCell').html(data);
                return false;
            } else {
                document.location.href = ref;
            }
        });
        return false;
    });

    $('a[arg="check"]').click(function () {
        var args = $(this).attr('fields').split(';');
        var correct = true;
        for (var i = 0; i < args.length; i++) {
            if (!$('#' + args[i]).length)
                correct = false;
            else correct = parseInt($('#' + args[i]).val()) > 0;
            if (!correct) {
                $('#messageCell').html($(this).attr('message'));
                return false;
            }
        }
        return true;
    });
    $('a[arg="check-text"]').click(function () {
        var correct = true;
        var required = $('input[required="1"]');
        required.each(function () {
            if (!$(this).val().length)
                correct = false;
        });
        $('#PersonalCorrect').val(correct ? "True" : "False");
        if (!correct) {
            $('#messageCell').html($(this).attr('message'));
            return false;
        }
        return true;
    });
}

function loadVerticalMenu() {
    $('#mega-1').dcVerticalMegaMenu({
        rowItems: '3',
        speed: 'fast',
        effect: 'slide',
        direction: 'right'
    });
}
function loadAutoSavers() {
    $('.personal-information input[name="DeliveryPayment"]').filter('[value="' + $('#DeliveryPayment').val() + '"]').attr('checked', 'checked');
    $('.personal-information input[name="DeliveryPayment"]').change(function () {
        $('#DeliveryPayment').val($(this).val());
    });
    $('a[rel="auto-save"]').click(function () {

        var items = new Array();
        $('.content-block input[type="text"], .content-block input[type="password"]').each(function () {
            items.push({ name: $(this).attr('id'), value: $(this).val() });
        });
        $('.content-block input[type="radio"]:checked').each(function () {
            items.push({ name: $(this).attr('id'), value: $(this).val() });
        });
        $('.personal-information input[type="text"], .personal-information input[type="hidden"], .personal-information textarea, .personal-information select').each(function () {
            items.push({ name: $(this).attr('id'), value: $(this).val() });
        });
        //var ref = $(this).attr('href');
        $.ajax({
            url: '/Master/ShopCart/autoSave',
            data: $.toJSON(items),
            type: 'POST',
            contentType: 'application/json',
            success: function (data) {
                //document.location.href = ref;
            },
            dataType: "html"
        });
        return true;
    });
}

function loadSubmit() {
    $('#complexFilterSubmit').click(function () {
        var tags = '';
        $(this).parents('form').find('select').each(function () {
            if ($(this).val() > 0)
                tags += $(this).val() + ',';
        });
        if (tags.length)
            document.location.href = location.protocol + '//' + location.host + location.pathname + '?tags=' + tags.substring(0, tags.length - 1);
        return false;
    });
}

function loadOrderTabs() {

    var tabContainers = $('div.tabs > div');
    var hash = document.location.hash;
    if (!hash.length)
        hash = "#first";
    $('div.tabs ul.tabNavigation a').click(function () {
        tabContainers.hide();
        tabContainers.filter(this.hash).show();
        $('div.tabs ul.tabNavigation a').removeClass('selected');
        $(this).addClass('selected');
        document.location.hash = $(this).attr('href');
        return false;
    });

    //tabContainers.hide().filter(hash).show();
    var btn = $('div.tabs ul.tabNavigation').find('a[href="' + hash + '"]');
    if (!btn.length) {
        btn = $('div.tabs ul.tabNavigation a').filter(':first');
        if (btn.length)
            document.location.hash = btn.attr('href');
    }
    if (btn.length)
        btn.click();

}

function loadShopCart() {
    $('.book-line .links a').click(function () {
        $.post('/Master/ShopCart/editItem', { act: $(this).attr('action'), id: $(this).attr('arg'), count: 0, isCart: document.location.href.indexOf('/order') >= 0 }, function (data) {
            var cell = $('.main');
            if (document.location.href.indexOf('/order') < 0)
                cell = $('.cart-cell');
            cell.replaceWith(data);
            $('.book-line .links a').unbind('click');
            loadShopCart();
            loadOrderTabs();
            loadBuyBtns();
        });
        return false;
    });
    $('.book-line .number input').blur(function () {
        var a = $(this).parents('.book-line').find('.links a:first');
        var cnt = 0;
        try {
            cnt = parseInt($(this).val());
        } catch (e) {
            cnt = 0;
        }
        $.post('/Master/ShopCart/editItem', { act: "change", id: a.attr('arg'), count: cnt, isCart: document.location.href.indexOf('/order') >= 0 }, function (data) {
            var cell = $('.main');
            if (document.location.href.indexOf('/order') < 0)
                cell = $('.cart-cell');
            cell.replaceWith(data);
            loadShopCart();
            loadOrderTabs();
        });

    });
}

function loadBuyBtns() {
    $('.basket_db').each(function () {
        if ($.trim($(this).html()) != '') {
            $(this).addClass('h50');
        }
    });
    $('a[rel="to-cart"]').click(function () {
        var arg = $(this).attr('arg');
        var spec = $(this).attr('spec');
        var count = 1;
        var pb = $(this).parents('#shop_block');
        if (pb != null && pb.length) {
            try {
                count = parseInt(pb.find('#count').val());
            }
            catch (e) {
                count = 1;
            }
        }
        var btn = $(this);
        $.post('/Master/Shopcart/toCart', { id: arg, count: count, spec:  spec == '1' }, function (data) {
            $('.basket').replaceWith(data);
            if (btn.hasClass('basket_db'))
                btn.addClass('h50');
            btn.html('<span class="already-in">Уже в корзине</span>');
            if (spec == '1') {
                if ($('.main').length) {
                    $.post('/Master/ShopCart/editItem', { act: "", id: 0, count: -1, isCart: document.location.href.indexOf('/order') >= 0 }, function (data) {
                        var cell = $('.main');
                        if (document.location.href.indexOf('/order') < 0)
                            cell = $('.cart-cell');
                        cell.replaceWith(data);
                        loadShopCart();
                        loadOrderTabs();
                        loadBuyBtns();
                    });
                }
            }
        });
        return false;
    });
}

function loadCounters() {
    $('.sprinter_number').each(function () {
        var num = parseInt($(this).attr('arg'));
        var empty = 6 - num.toString().length;
        for (var i = 0; i < empty; i++) {
            $(this).find('p').before('<span class="n"><span></span></span>');
        }
        var sumLen = num.toString().length;
        for (var j = 0; j < sumLen; j++) {
            $(this).find('p').before('<span class="n"><span></span>' + num.toString()[j] + '</span>');
        }

    });
}

function loadMarks() {
    var dataSaveUrl = '/Master/ClientCatalog/SaveMark';
    var stars = $('.stars');
    var marks = $.cookie('marked');
    if (marks == null) marks = '';
    var array = marks.split(',');
    var av = parseInt(stars.attr('val'));
    for (var i = 0; i < 5; i++) {
        stars.append('<a class="' + (i < av ? 'on' : 'off') + '_star" arg=' + (i + 1) + ' href="javascript:void(0);"></a>');
    }
    if ($.inArray(stars.attr('arg'), array) < 0) {
        stars.find('a').fadeTo(0, 0.7);
        stars.mouseout(function () {
            av = parseInt(stars.attr('val'));
            if (av == 0) {
                stars.find('a').attr('class', 'off_star');
            } else {
                stars.find('a:lt(' + (av) + ')').attr('class', 'on_star');
                stars.find('a:gt(' + (av - 1) + ')').attr('class', 'off_star');
            }
        });
        stars.find('a').mouseover(function () {
            stars.find('a:lt(' + (parseInt($(this).attr('arg')) + 2) + ')').attr('class', 'on_star');
            stars.find('a:gt(' + (parseInt($(this).attr('arg')) - 1) + ')').attr('class', 'off_star');
        }).click(function () {
            $.post(dataSaveUrl, { mark: $(this).attr('arg'), book: stars.attr('arg') }, function (data) {
                if (data == '-1') {
                    alert("Еще раз попробуешь - анально покараю!");
                    return;
                } else {
                    stars.attr('val', data);
                    av = parseInt(data);
                    stars.find('a:lt(' + (av) + ')').attr('class', 'on_star');
                    stars.find('a:gt(' + (av - 1) + ')').attr('class', 'off_star');
                    var els = stars.find('a');
                    els.fadeTo(100, 1);
                    els.unbind('click');
                    els.unbind('mouseover');
                    stars.unbind('mouseout');
                    $.cookie('marked', $.cookie('marked') + ',' + stars.attr('arg'), { expires: 1000, path: '/' });
                }
            });
            return false;
        });
    }

}

function loadSwitcher() {
    $('div [id^="switcher"]').each(function () {
        $(this).find('a').click(function () {
            var a = $(this);
            if (a.parent().hasClass('ml_active')) return false;
            var savename = $(this).parents('div [id^="switcher"]').attr('savename');
            $('#ajax-content[savename="' + savename + '"]').fadeTo(0, 0.5);

            $.get(a.attr('href'), {}, function (data) {

                $.cookie(savename, a.attr('num'), { expires: 7, path: '/' });
                var content = a.parents('.content_block').find('#ajax-content[savename="' + savename + '"]');
                content.html(data);
                content.fadeTo(0, 1);
                a.parents('[id^="switcher"]').find('li').attr('class', 'ml_link');
                a.parent().attr('class', 'ml_active');
                a.parents('[id^="switcher"]').find('a').attr('class', 'ml_link');
                a.attr('class', '');
                a.parents('[id^="switcher"]').find('.arrow').remove();
                a.html(a.text() + '<span class="arrow"></span>');
                if ($('.big_book').length && $('.big_book').is(":hidden"))
                    $('.big_book').show();
                if ($('.big_book2').length && $('.big_book2').is(":hidden"))
                    $('.big_book2').show();
                if ($('#switcher_lower').length && $('#switcher_lower').is(":hidden"))
                    $('#switcher_lower').show();

                loadOneClickBuy(content);
            });
            return false;
        });
    });


    $('div [id^="switcher"]').each(function () {
        var savename = $(this).attr('savename');
        var num = 0;
        if ($.cookie(savename)) num = $.cookie(savename);
        $(this).find('a[num="' + num + '"]').click();

    });
}